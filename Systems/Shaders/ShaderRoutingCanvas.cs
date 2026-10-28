using System;
using System.Collections.Generic;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;

namespace Xanadu.Systems.Shaders;

/// <summary>Makes and styles the canvas items shaded draws go to.</summary>
public interface IShaderCanvasItems
{
    Rid CreateItem(Rid parent);

    /// <summary>Gives an item the material of a shader slot, or none for slot 0.</summary>
    void SetMaterial(Rid item, int slot);
}

/// <summary>
/// Wraps a canvas so some of what is drawn into one canvas item can use a
/// shader. A canvas item has a single material, so draws are split into runs:
/// each time the selected slot changes, drawing moves to a fresh child item
/// with that slot's material. Children draw in the order they were made, so
/// the objects still stack exactly as they were drawn.
/// </summary>
/// <remarks>
/// Once any shaded run exists, later unshaded draws also move to child items,
/// since the parent item itself always draws underneath its children.
/// </remarks>
public sealed class ShaderRoutingCanvas(IRectCanvas inner, Rid root, IShaderCanvasItems items) : IRectCanvas
{
    private readonly List<Rid> _pool = new();
    private int _used;
    private int _slot;
    private int _runSlot = -1;
    private Rid _run;
    private bool _hasRun;
    private bool _shadedSeen;

    /// <summary>How many child items this frame's draws needed.</summary>
    public int ItemsUsed => _used;

    public void SelectShader(int slot) => _slot = slot;

    public void Clear(Rid canvas)
    {
        inner.Clear(canvas);
        if (canvas != root)
            return;

        foreach (Rid item in _pool)
            inner.Clear(item);

        _used = 0;
        _slot = 0;
        _runSlot = -1;
        _hasRun = false;
        _shadedSeen = false;
    }

    /// <summary>The item a draw aimed at <paramref name="canvas"/> should land in.</summary>
    private Rid Route(Rid canvas)
    {
        if (canvas != root)
            return canvas;

        if (!_shadedSeen && _slot == 0)
            return root;

        if (_hasRun && _runSlot == _slot)
            return _run;

        if (_used == _pool.Count)
            _pool.Add(items.CreateItem(root));

        _run = _pool[_used++];
        _runSlot = _slot;
        _hasRun = true;
        _shadedSeen = true;
        items.SetMaterial(_run, _slot);
        return _run;
    }

    public void SetVisible(Rid canvas, bool visible) => inner.SetVisible(canvas, visible);

    public void SetTransform(Rid canvas, Transform2D transform) => inner.SetTransform(canvas, transform);

    public void DrawRect(Rid canvas, Rect2 bounds, Color color) =>
        inner.DrawRect(Route(canvas), bounds, color);

    public void DrawTexture(Rid canvas, Rect2 bounds, Texture2D texture) =>
        inner.DrawTexture(Route(canvas), bounds, texture);

    public void DrawCircle(Rid canvas, Vector2 center, float radius, Color color) =>
        inner.DrawCircle(Route(canvas), center, radius, color);

    public void DrawPolygon(Rid canvas, ReadOnlySpan<Vector2> points, Color color) =>
        inner.DrawPolygon(Route(canvas), points, color);

    public void DrawPolyline(
        Rid canvas,
        ReadOnlySpan<Vector2> points,
        Color color,
        float width,
        bool antialiased = true) =>
        inner.DrawPolyline(Route(canvas), points, color, width, antialiased);

    public void DrawCircleOutline(
        Rid canvas,
        Vector2 center,
        float radius,
        Color color,
        float width,
        int segments = 40) =>
        inner.DrawCircleOutline(Route(canvas), center, radius, color, width, segments);

    public void DrawLine(Rid canvas, Vector2 from, Vector2 to, Color color, float width) =>
        inner.DrawLine(Route(canvas), from, to, color, width);

    public void DrawText(Rid canvas, Rect2 bounds, string text, Color color) =>
        inner.DrawText(Route(canvas), bounds, text, color);

    public void DrawTextStyled(Rid canvas, Rect2 bounds, string text, Color color, int fontSize) =>
        inner.DrawTextStyled(Route(canvas), bounds, text, color, fontSize);

    public void DrawTextLeft(Rid canvas, Rect2 bounds, string text, Color color) =>
        inner.DrawTextLeft(Route(canvas), bounds, text, color);

    public void DrawTextRight(Rid canvas, Rect2 bounds, string text, Color color) =>
        inner.DrawTextRight(Route(canvas), bounds, text, color);

    public void DrawTextVertical(Rid canvas, Rect2 bounds, string text, Color color, int fontSize) =>
        inner.DrawTextVertical(Route(canvas), bounds, text, color, fontSize);

    public void DrawTextStyledLeft(Rid canvas, Rect2 bounds, string text, Color color, int fontSize) =>
        inner.DrawTextStyledLeft(Route(canvas), bounds, text, color, fontSize);

    public void DrawTextStyledRight(Rid canvas, Rect2 bounds, string text, Color color, int fontSize) =>
        inner.DrawTextStyledRight(Route(canvas), bounds, text, color, fontSize);

    public void DrawTextWeighted(
        Rid canvas,
        Rect2 bounds,
        string text,
        Color color,
        int fontSize,
        TextAlign align,
        bool bold) =>
        inner.DrawTextWeighted(Route(canvas), bounds, text, color, fontSize, align, bold);

    public void DrawTextGlow(
        Rid canvas,
        Rect2 bounds,
        string text,
        Color color,
        int fontSize,
        TextAlign align,
        bool bold,
        TextGlow glow,
        TextOutline? outline = null) =>
        inner.DrawTextGlow(Route(canvas), bounds, text, color, fontSize, align, bold, glow, outline);

    public void DrawShapeGlow(
        Rid canvas,
        ReadOnlySpan<Vector2> points,
        bool closed,
        bool filled,
        float strokeWidth,
        ShapeGlow glow) =>
        inner.DrawShapeGlow(Route(canvas), points, closed, filled, strokeWidth, glow);

    public void DrawTextOutline(
        Rid canvas,
        Rect2 bounds,
        string text,
        Color color,
        int fontSize,
        TextAlign align,
        bool bold,
        int size) =>
        inner.DrawTextOutline(Route(canvas), bounds, text, color, fontSize, align, bold, size);
}

/// <summary>Shader canvas items backed by the rendering server.</summary>
public sealed class GodotShaderCanvasItems : IShaderCanvasItems
{
    /// <summary>Looks up the material of a slot; set each frame from the level being drawn.</summary>
    public Func<int, Rid> MaterialOf { get; set; } = static _ => default;

    public Rid CreateItem(Rid parent)
    {
        Rid item = RenderingServer.CanvasItemCreate();
        RenderingServer.CanvasItemSetParent(item, parent);
        return item;
    }

    public void SetMaterial(Rid item, int slot) =>
        RenderingServer.CanvasItemSetMaterial(item, slot == 0 ? default : MaterialOf(slot));
}

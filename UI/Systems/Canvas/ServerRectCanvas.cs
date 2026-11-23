using System.Collections.Generic;
using Xanadu.Core.Rendering;
using System;
using Xanadu.Core.Maths;

namespace Xanadu.Ui.Systems.Canvas;

/// <summary>
/// The shape half of the production canvas: every draw goes to the
/// <see cref="CanvasServer"/> as a command on the canvas item. Text and glows
/// are left to subclasses (the host's text, Docs/host.md §5.4), so this draws
/// no text on its own.
/// </summary>
public class ServerRectCanvas : IRectCanvas
{
    protected static ICanvasServer Server => CanvasServer.Current;

    private readonly Dictionary<(string, BlendMode), CanvasMaterial> _materials = new();
    private CanvasMaterial? _material;
    private Dictionary<string, Vector4>? _values;

    /// <summary>Shades the draws that follow with a common shader by path (none for an empty path), in a run of their own.</summary>
    public void SelectCommonShader(string path, BlendMode blend = BlendMode.Mix)
    {
        if (path.Length == 0)
        {
            _material = null;
            _values = null;
            return;
        }

        if (!_materials.TryGetValue((path, blend), out CanvasMaterial? material))
            _materials[(path, blend)] = material = new CanvasMaterial(path, blend);
        _material = material;
        _values = null;
    }

    /// <summary>Level shader slots went with the v2 rework: slot 0 (plain) is the only one left.</summary>
    public void SelectShader(int slot, BlendMode blend = BlendMode.Mix, int sourceGroup = -1)
    {
        if (slot == 0)
            SelectCommonShader("");
    }

    /// <summary>A value for the shaded draws that follow, in the current run.</summary>
    public void SetShaderValue(string param, Vector4 value, int size) => (_values ??= new Dictionary<string, Vector4>())[param] = value;

    /// <summary>Starts a new run even with the same shader, so values set now reach only the draws that follow.</summary>
    public void NewRun() => _values = new Dictionary<string, Vector4>();

    /// <summary>Puts the item's next draws in the selected run, before every draw.</summary>
    protected void Route(Handle canvas) => Server.SelectRun(canvas, _material, _values);

    public virtual void Clear(Handle canvas) => Server.Clear(canvas);

    public virtual void DrawText(Handle canvas, Rect2 bounds, string text, Color color) { }

    public virtual void DrawTextLeft(Handle canvas, Rect2 bounds, string text, Color color) { }

    public virtual void DrawTextRight(Handle canvas, Rect2 bounds, string text, Color color) { }

    public void SetVisible(Handle canvas, bool visible) =>
        Server.SetVisible(canvas, visible);

    public void SetTransform(Handle canvas, Transform2D transform) =>
        Server.SetTransform(canvas, transform);

    public void DrawTexture(Handle canvas, Rect2 bounds, Handle texture)
    {
        Route(canvas);
        Server.AddTextureRect(canvas, bounds, texture);
    }

    public void DrawRect(Handle canvas, Rect2 bounds, Color color)
    {
        Route(canvas);
        Server.AddRect(canvas, bounds, color);
    }

    public void DrawCircle(
        Handle canvas,
        Vector2 center,
        float radius,
        Color color)
    {
        Route(canvas);
        Server.AddCircle(canvas, center, radius, color);
    }

    public void DrawLine(Handle canvas, Vector2 from, Vector2 to, Color color, float width)
    {
        Route(canvas);
        Server.AddLine(canvas, from, to, color, width, true);
    }

    public void DrawGradientRect(Handle canvas, Rect2 bounds, Color start, Color end, bool horizontal)
    {
        Route(canvas);
        Vector2 topRight = new(bounds.End.X, bounds.Position.Y);
        Vector2 bottomLeft = new(bounds.Position.X, bounds.End.Y);
        Server.AddPolygon(
            canvas,
            new[] { bounds.Position, topRight, bounds.End, bottomLeft },
            horizontal
                ? new[] { start, end, end, start }
                : new[] { start, start, end, end });
    }

    public void DrawPolygon(Handle canvas, ReadOnlySpan<Vector2> points, Color color)
    {
        Route(canvas);
        if (points.Length < 3)
            return;

        Vector2[] vertices = points.ToArray();
        var colors = new Color[vertices.Length];
        Array.Fill(colors, color);
        Server.AddPolygon(canvas, vertices, colors);
    }

    public void DrawTriangles(Handle canvas, ReadOnlySpan<Vector2> points, ReadOnlySpan<Color> colors)
    {
        Route(canvas);
        int count = points.Length - points.Length % 3;
        if (count < 3 || colors.Length < count)
            return;

        var indices = new int[count];
        for (int i = 0; i < count; i++)
            indices[i] = i;
        Server.AddTriangleArray(
            canvas, indices, points[..count].ToArray(), colors[..count].ToArray());
    }

    public void DrawTriangles(Handle canvas, ReadOnlySpan<Vector2> points, ReadOnlySpan<Color> colors, ReadOnlySpan<Vector2> uvs)
    {
        Route(canvas);
        int count = points.Length - points.Length % 3;
        if (count < 3 || colors.Length < count || uvs.Length < count)
            return;

        var indices = new int[count];
        for (int i = 0; i < count; i++)
            indices[i] = i;
        Server.AddTriangleArray(
            canvas, indices, points[..count].ToArray(), colors[..count].ToArray(), uvs[..count].ToArray());
    }

    public void DrawCurveTriangles(Handle canvas, ReadOnlySpan<Vector2> points, ReadOnlySpan<Color> colors, ReadOnlySpan<Vector2> uvs, ReadOnlySpan<byte> curves)
    {
        Route(canvas);
        int count = points.Length - points.Length % 3;
        if (count < 3 || colors.Length < count || uvs.Length < count)
            return;

        Server.AddCurveTriangles(canvas, points[..count].ToArray(), colors[..count].ToArray(), uvs[..count].ToArray(), curves.ToArray());
    }

    public void DrawRectOutlines(Handle canvas, ReadOnlySpan<Rect2> rects, Color color)
    {
        Route(canvas);
        if (rects.IsEmpty)
            return;

        var points = new Vector2[rects.Length * 8];
        int next = 0;
        foreach (Rect2 rect in rects)
        {
            Vector2 topRight = new(rect.End.X, rect.Position.Y);
            Vector2 bottomLeft = new(rect.Position.X, rect.End.Y);
            points[next++] = rect.Position;
            points[next++] = topRight;
            points[next++] = topRight;
            points[next++] = rect.End;
            points[next++] = rect.End;
            points[next++] = bottomLeft;
            points[next++] = bottomLeft;
            points[next++] = rect.Position;
        }

        // A negative width is the renderer's plain one-pixel line, its cheapest kind.
        Server.AddMultiline(canvas, points, new[] { color }, -1f);
    }

    public void DrawPolyline(
        Handle canvas,
        ReadOnlySpan<Vector2> points,
        Color color,
        float width,
        bool antialiased = true)
    {
        Route(canvas);
        if (points.Length < 2)
            return;

        Vector2[] vertices = points.ToArray();
        var colors = new Color[vertices.Length];
        Array.Fill(colors, color);
        Server.AddPolyline(
            canvas,
            vertices,
            colors,
            width,
            antialiased);
    }
}

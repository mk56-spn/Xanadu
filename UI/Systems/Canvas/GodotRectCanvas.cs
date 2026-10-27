using System;
using Godot;
using Xanadu.Ecs;

namespace Xanadu.Systems.Canvas;

/// <summary>Production canvas operations backed by Godot's RenderingServer.</summary>
public sealed class GodotRectCanvas : IRectCanvas
{
    private const int FontSize = 16;
    private const string BoldFontPath = "res://Assets/Fonts/SourceSans3-Variable.ttf";
    private const int BoldWeight = 700;

    private static Font? _boldFont;

    /// <summary>
    /// Source Sans 3 at weight 700, or the fallback font if the file cannot
    /// be loaded. The file is a variable font whose default weight is light.
    /// </summary>
    private static Font BoldFont => _boldFont ??= LoadBoldFont();

    private static Font LoadBoldFont()
    {
        if (GD.Load<FontFile>(BoldFontPath) is not { } file)
        {
            GD.PushError($"Bold font '{BoldFontPath}' not found; using the default font.");
            return ThemeDB.FallbackFont;
        }

        return new FontVariation
        {
            BaseFont = file,
            VariationOpentype = new Godot.Collections.Dictionary
            {
                { TextServerManager.GetPrimaryInterface().NameToTag("wght"), BoldWeight },
            },
        };
    }
    private const float TextPadding = 4;

    private readonly GodotTextGlow _glow = new();

    public void SetVisible(Rid canvas, bool visible) =>
        RenderingServer.CanvasItemSetVisible(canvas, visible);

    public void SetTransform(Rid canvas, Transform2D transform) =>
        RenderingServer.CanvasItemSetTransform(canvas, transform);

    public void Clear(Rid canvas)
    {
        RenderingServer.CanvasItemClear(canvas);
        _glow.Clear(canvas);
        if (!_rotated.TryGetValue(canvas, out RotatedPool? pool))
            return;

        foreach (Rid item in pool.Items)
            RenderingServer.CanvasItemClear(item);
        pool.Used = 0;
    }

    public void DrawRect(Rid canvas, Rect2 bounds, Color color) =>
        RenderingServer.CanvasItemAddRect(canvas, bounds, color);

    public void DrawCircle(
        Rid canvas,
        Vector2 center,
        float radius,
        Color color) =>
        RenderingServer.CanvasItemAddCircle(canvas, center, radius, color, true);

    public void DrawTexture(Rid canvas, Rect2 bounds, Texture2D texture) =>
        RenderingServer.CanvasItemAddTextureRect(
            canvas,
            bounds,
            texture.GetRid());

    public void DrawLine(Rid canvas, Vector2 from, Vector2 to, Color color, float width) =>
        RenderingServer.CanvasItemAddLine(canvas, from, to, color, width, true);

    public void DrawPolygon(Rid canvas, ReadOnlySpan<Vector2> points, Color color)
    {
        if (points.Length < 3)
            return;

        Vector2[] vertices = points.ToArray();
        var colors = new Color[vertices.Length];
        Array.Fill(colors, color);
        RenderingServer.CanvasItemAddPolygon(canvas, vertices, colors);
    }

    public void DrawPolyline(
        Rid canvas,
        ReadOnlySpan<Vector2> points,
        Color color,
        float width,
        bool antialiased = true)
    {
        if (points.Length < 2)
            return;

        Vector2[] vertices = points.ToArray();
        var colors = new Color[vertices.Length];
        Array.Fill(colors, color);
        RenderingServer.CanvasItemAddPolyline(
            canvas,
            vertices,
            colors,
            width,
            antialiased);
    }

    public void DrawText(Rid canvas, Rect2 bounds, string text, Color color) =>
        DrawTextAligned(canvas, bounds, text, color, FontSize, HorizontalAlignment.Center);

    public void DrawTextStyled(
        Rid canvas,
        Rect2 bounds,
        string text,
        Color color,
        int fontSize) =>
        DrawTextAligned(canvas, bounds, text, color, fontSize, HorizontalAlignment.Center);

    public void DrawTextStyledLeft(
        Rid canvas,
        Rect2 bounds,
        string text,
        Color color,
        int fontSize) =>
        DrawTextAligned(canvas, bounds, text, color, fontSize, HorizontalAlignment.Left);

    public void DrawTextStyledRight(
        Rid canvas,
        Rect2 bounds,
        string text,
        Color color,
        int fontSize) =>
        DrawTextAligned(canvas, bounds, text, color, fontSize, HorizontalAlignment.Right);

    /// <summary>
    /// Rotated child items for vertical text, one per label drawn since the
    /// canvas was last cleared, so several labels can be on screen at once.
    /// </summary>
    private readonly System.Collections.Generic.Dictionary<Rid, RotatedPool> _rotated = new();

    private sealed class RotatedPool
    {
        public readonly System.Collections.Generic.List<Rid> Items = new();
        public int Used;
    }

    public void DrawTextVertical(
        Rid canvas,
        Rect2 bounds,
        string text,
        Color color,
        int fontSize)
    {
        if (string.IsNullOrEmpty(text))
            return;

        if (!_rotated.TryGetValue(canvas, out RotatedPool? pool))
        {
            pool = new RotatedPool();
            _rotated.Add(canvas, pool);
        }

        if (pool.Used == pool.Items.Count)
        {
            Rid created = RenderingServer.CanvasItemCreate();
            RenderingServer.CanvasItemSetParent(created, canvas);
            RenderingServer.CanvasItemSetZIndex(created, 2);
            pool.Items.Add(created);
        }

        Rid item = pool.Items[pool.Used++];
        RenderingServer.CanvasItemClear(item);
        RenderingServer.CanvasItemSetTransform(
            item,
            new Transform2D(-Mathf.Pi / 2, new Vector2(
                bounds.Position.X + 2,
                bounds.End.Y - 2)));

        Font font = ThemeDB.FallbackFont;
        font.DrawString(
            item,
            new Vector2(0, fontSize),
            text,
            HorizontalAlignment.Left,
            Mathf.Max(0, bounds.Size.Y - 4),
            fontSize,
            color);
    }

    public void DrawTextWeighted(
        Rid canvas,
        Rect2 bounds,
        string text,
        Color color,
        int fontSize,
        TextAlign align,
        bool bold) =>
        DrawTextAligned(
            canvas,
            bounds,
            text,
            color,
            fontSize,
            align switch
            {
                TextAlign.Left => HorizontalAlignment.Left,
                TextAlign.Right => HorizontalAlignment.Right,
                _ => HorizontalAlignment.Center,
            },
            bold ? BoldFont : ThemeDB.FallbackFont);

    public void DrawTextGlow(
        Rid canvas,
        Rect2 bounds,
        string text,
        Color color,
        int fontSize,
        TextAlign align,
        bool bold,
        TextGlow glow) =>
        _glow.Draw(
            canvas,
            bounds,
            text,
            color,
            fontSize,
            align switch
            {
                TextAlign.Left => HorizontalAlignment.Left,
                TextAlign.Right => HorizontalAlignment.Right,
                _ => HorizontalAlignment.Center,
            },
            bold ? BoldFont : ThemeDB.FallbackFont,
            glow);

    public void DrawTextLeft(Rid canvas, Rect2 bounds, string text, Color color) =>
        DrawTextAligned(canvas, bounds, text, color, FontSize, HorizontalAlignment.Left);

    public void DrawTextRight(Rid canvas, Rect2 bounds, string text, Color color) =>
        DrawTextAligned(canvas, bounds, text, color, FontSize, HorizontalAlignment.Right);

    internal static void DrawTextAligned(
        Rid canvas,
        Rect2 bounds,
        string text,
        Color color,
        int fontSize,
        HorizontalAlignment alignment,
        Font? face = null)
    {
        if (string.IsNullOrEmpty(text))
            return;

        Font font = face ?? ThemeDB.FallbackFont;
        float baseline = bounds.Position.Y +
                         (bounds.Size.Y - font.GetHeight(fontSize)) / 2 +
                         font.GetAscent(fontSize);

        font.DrawString(
            canvas,
            new Vector2(bounds.Position.X + TextPadding, baseline),
            text,
            alignment,
            Mathf.Max(0, bounds.Size.X - TextPadding * 2),
            fontSize,
            color);
    }
}

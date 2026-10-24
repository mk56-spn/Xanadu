using System;
using Godot;

namespace Writer.Systems.Canvas;

/// <summary>Production canvas operations backed by Godot's RenderingServer.</summary>
public sealed class GodotRectCanvas : IRectCanvas
{
    private const int FontSize = 16;
    private const float TextPadding = 4;

    public void SetVisible(Rid canvas, bool visible) =>
        RenderingServer.CanvasItemSetVisible(canvas, visible);

    public void SetTransform(Rid canvas, Transform2D transform) =>
        RenderingServer.CanvasItemSetTransform(canvas, transform);

    public void Clear(Rid canvas) => RenderingServer.CanvasItemClear(canvas);

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

    private readonly System.Collections.Generic.Dictionary<Rid, Rid> _rotated = new();

    public void DrawTextVertical(
        Rid canvas,
        Rect2 bounds,
        string text,
        Color color,
        int fontSize)
    {
        if (string.IsNullOrEmpty(text))
            return;

        if (!_rotated.TryGetValue(canvas, out Rid item))
        {
            item = RenderingServer.CanvasItemCreate();
            RenderingServer.CanvasItemSetParent(item, canvas);
            RenderingServer.CanvasItemSetZIndex(item, 2);
            _rotated.Add(canvas, item);
        }

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

    public void DrawTextLeft(Rid canvas, Rect2 bounds, string text, Color color) =>
        DrawTextAligned(canvas, bounds, text, color, FontSize, HorizontalAlignment.Left);

    public void DrawTextRight(Rid canvas, Rect2 bounds, string text, Color color) =>
        DrawTextAligned(canvas, bounds, text, color, FontSize, HorizontalAlignment.Right);

    private static void DrawTextAligned(
        Rid canvas,
        Rect2 bounds,
        string text,
        Color color,
        int fontSize,
        HorizontalAlignment alignment)
    {
        if (string.IsNullOrEmpty(text))
            return;

        Font font = ThemeDB.FallbackFont;
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

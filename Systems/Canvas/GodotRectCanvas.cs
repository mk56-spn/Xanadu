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

    public void DrawLine(Rid canvas, Vector2 from, Vector2 to, Color color, float width) =>
        RenderingServer.CanvasItemAddLine(canvas, from, to, color, width, true);

    public void DrawText(Rid canvas, Rect2 bounds, string text, Color color) =>
        DrawTextAligned(canvas, bounds, text, color, FontSize, HorizontalAlignment.Center);

    public void DrawTextStyled(
        Rid canvas,
        Rect2 bounds,
        string text,
        Color color,
        int fontSize) =>
        DrawTextAligned(canvas, bounds, text, color, fontSize, HorizontalAlignment.Center);

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

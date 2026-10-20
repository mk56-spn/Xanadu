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

    public void DrawText(Rid canvas, Rect2 bounds, string text, Color color)
    {
        if (string.IsNullOrEmpty(text))
            return;

        Font font = ThemeDB.FallbackFont;
        float baseline = bounds.Position.Y +
                         (bounds.Size.Y - font.GetHeight(FontSize)) / 2 +
                         font.GetAscent(FontSize);

        font.DrawString(
            canvas,
            new Vector2(bounds.Position.X + TextPadding, baseline),
            text,
            HorizontalAlignment.Center,
            Mathf.Max(0, bounds.Size.X - TextPadding * 2),
            FontSize,
            color);
    }
}

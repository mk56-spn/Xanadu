using Godot;
using Xanadu.Ecs;

namespace Xanadu.Systems.Canvas;

/// <summary>Shared drawing of the box a view drags to select, so every view's looks the same.</summary>
public static class CanvasMarquee
{
    public static readonly Color FillColor = new(0.25f, 0.55f, 1f, 0.12f);
    public static readonly Color BorderColor = new(0.45f, 0.7f, 1f, 0.9f);

    /// <summary>Draws the box while it is active.</summary>
    public static void Draw(IRectCanvas canvas, Rid rid, in UiMarqueeBox box)
    {
        if (box.Active)
            Draw(canvas, rid, box.Bounds);
    }

    public static void Draw(IRectCanvas canvas, Rid rid, Rect2 bounds)
    {
        canvas.DrawRect(rid, bounds, FillColor);

        // One-pixel border from four strips, as the canvas has no outline for rects.
        canvas.DrawRect(rid, new Rect2(bounds.Position, new Vector2(bounds.Size.X, 1)), BorderColor);
        canvas.DrawRect(rid,
            new Rect2(new Vector2(bounds.Position.X, bounds.End.Y - 1), new Vector2(bounds.Size.X, 1)),
            BorderColor);
        canvas.DrawRect(rid, new Rect2(bounds.Position, new Vector2(1, bounds.Size.Y)), BorderColor);
        canvas.DrawRect(rid,
            new Rect2(new Vector2(bounds.End.X - 1, bounds.Position.Y), new Vector2(1, bounds.Size.Y)),
            BorderColor);
    }
}

using Godot;

namespace Xanadu.Systems.Canvas;

/// <summary>Outlines drawn from a canvas's filled rectangles.</summary>
public static class CanvasBorder
{
    /// <summary>Draws a border of <paramref name="width"/> just inside <paramref name="bounds"/>; its sides do not overlap, so a see-through colour stays even at the corners.</summary>
    public static void DrawBorder(
        this IRectCanvas canvas,
        Rid rid,
        Rect2 bounds,
        Color color,
        float width)
    {
        canvas.DrawRect(rid,
            new Rect2(bounds.Position, new Vector2(bounds.Size.X, width)), color);
        canvas.DrawRect(rid,
            new Rect2(new Vector2(bounds.Position.X, bounds.End.Y - width),
                new Vector2(bounds.Size.X, width)), color);
        canvas.DrawRect(rid,
            new Rect2(bounds.Position + new Vector2(0, width), new Vector2(width, bounds.Size.Y - width * 2)), color);
        canvas.DrawRect(rid,
            new Rect2(new Vector2(bounds.End.X - width, bounds.Position.Y + width),
                new Vector2(width, bounds.Size.Y - width * 2)), color);
    }
}

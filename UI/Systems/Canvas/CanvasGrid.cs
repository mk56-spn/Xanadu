using Godot;

namespace Writer.Systems.Canvas;

/// <summary>
/// Shared grid drawing for editor-style views. Lines are spaced one cell
/// apart and aligned so a pair of them passes through the axis point; those
/// two axis lines are drawn heavier.
/// </summary>
public static class CanvasGrid
{
    public static readonly Color LineColor = new("#273449");
    public static readonly Color AxisColor = new("#64748b");

    /// <summary>Tolerance so lines landing exactly on the bounds edge are kept.</summary>
    private const float EdgeEpsilon = 0.01f;

    /// <summary>Draws grid lines across <paramref name="bounds"/>.</summary>
    public static void Draw(
        IRectCanvas canvas,
        Rid rid,
        Rect2 bounds,
        Vector2 cell,
        Vector2 axis)
    {
        if (cell.X <= 0 || cell.Y <= 0)
            return;

        (int first, int last) = LineRange(bounds.Position.X, bounds.End.X, axis.X, cell.X);
        for (int i = first; i <= last; i++)
        {
            float x = axis.X + i * cell.X;
            canvas.DrawLine(
                rid,
                new Vector2(x, bounds.Position.Y),
                new Vector2(x, bounds.End.Y),
                i == 0 ? AxisColor : LineColor,
                i == 0 ? 2 : 1);
        }

        (first, last) = LineRange(bounds.Position.Y, bounds.End.Y, axis.Y, cell.Y);
        for (int i = first; i <= last; i++)
        {
            float y = axis.Y + i * cell.Y;
            canvas.DrawLine(
                rid,
                new Vector2(bounds.Position.X, y),
                new Vector2(bounds.End.X, y),
                i == 0 ? AxisColor : LineColor,
                i == 0 ? 2 : 1);
        }
    }

    /// <summary>Cell indices, relative to the axis, of the lines inside [start, end].</summary>
    private static (int First, int Last) LineRange(float start, float end, float axis, float cell) =>
        (Mathf.CeilToInt((start - axis) / cell - EdgeEpsilon),
            Mathf.FloorToInt((end - axis) / cell + EdgeEpsilon));
}

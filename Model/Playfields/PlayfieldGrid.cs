using Xanadu.Model.Ecs.Components;

namespace Xanadu.Model.Playfields;

/// <summary>
/// The playfield's drawing grid, shared by the editor's snapping, the drawn
/// lines, and grid-sized items such as spikes.
/// </summary>
public static class PlayfieldGrid
{
    /// <summary>Cells per axis for every <see cref="Span"/> units of field.</summary>
    public const int Divisions = 20;

    /// <summary>True when a playfield point is inside the field.</summary>
    public static bool IsInside(Vector2 position, Playfield playfield) =>
        position.X >= 0 &&
        position.Y >= 0 &&
        position.X <= playfield.Width &&
        position.Y <= playfield.Height;

    /// <summary>
    /// The field length that gets <see cref="Divisions"/> cells. A field up to
    /// about this size is split into exactly that many; a bigger one gets that
    /// many per span, so its cells stay about the same size instead of
    /// stretching with the field.
    /// </summary>
    public const float Span = 1000f;

    /// <summary>Cells along an axis of <paramref name="size"/> units; always even, so the centre lines are cell edges.</summary>
    public static int DivisionsFor(int size) => Divisions * Math.Max(1, (int)MathF.Round(size / Span));

    public static Vector2 Cell(Playfield playfield) => new(
        playfield.Width / (float)DivisionsFor(playfield.Width),
        playfield.Height / (float)DivisionsFor(playfield.Height));

    /// <summary>
    /// The grid crossing nearest <paramref name="point"/>, inside the field: where
    /// a point item such as a note lands with snapping on.
    /// </summary>
    public static Vector2 NearestCrossing(Vector2 point, Playfield playfield)
    {
        Vector2 cell = Cell(playfield);
        if (cell.X <= 0 || cell.Y <= 0)
            return point;

        return new Vector2(
            Math.Clamp(MathF.Round(point.X / cell.X) * cell.X, 0, playfield.Width),
            Math.Clamp(MathF.Round(point.Y / cell.Y) * cell.Y, 0, playfield.Height));
    }

    /// <summary>The top-left of the cell containing <paramref name="point"/>.</summary>
    public static Vector2 Snap(Vector2 point, Playfield playfield)
    {
        Vector2 cell = Cell(playfield);
        if (cell.X <= 0 || cell.Y <= 0)
            return point;

        return new Vector2(
            MathF.Floor(point.X / cell.X) * cell.X,
            MathF.Floor(point.Y / cell.Y) * cell.Y);
    }
}

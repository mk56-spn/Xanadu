using Godot;
using Xanadu.Ecs;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// The playfield's drawing grid, shared by the editor's snapping, the drawn
/// lines, and grid-sized items such as spikes.
/// </summary>
public static class PlayfieldGrid
{
    /// <summary>Cells per axis.</summary>
    public const int Divisions = 20;

    public static Vector2 Cell(Playfield playfield) => new(
        playfield.Width / (float)Divisions,
        playfield.Height / (float)Divisions);

    /// <summary>The top-left of the cell containing <paramref name="point"/>.</summary>
    public static Vector2 Snap(Vector2 point, Playfield playfield)
    {
        Vector2 cell = Cell(playfield);
        if (cell.X <= 0 || cell.Y <= 0)
            return point;

        return new Vector2(
            Mathf.Floor(point.X / cell.X) * cell.X,
            Mathf.Floor(point.Y / cell.Y) * cell.Y);
    }
}

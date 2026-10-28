using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Playfields;

namespace Xanadu.Editor;

/// <summary>
/// The playfield grid blocks snap to. It is the same grid the editor draws:
/// the field is divided into <see cref="Divisions"/> cells per axis, and the
/// axis lines passing through the field centre land on cell boundaries, so
/// every drawn line sits on a whole multiple of the cell size.
/// </summary>
public static class BlockGrid
{
    /// <summary>Cells per axis, shared with the drawn grid and spike tiles.</summary>
    public const int Divisions = PlayfieldGrid.Divisions;

    public static Vector2 Cell(Playfield playfield) => PlayfieldGrid.Cell(playfield);

    /// <summary>Nearest grid line, rounding halves up so snapping is predictable.</summary>
    public static float Snap(float value, float cell) =>
        cell <= 0 ? value : Mathf.Floor(value / cell + 0.5f) * cell;

    public static Vector2 Snap(Vector2 value, Vector2 cell) => new(
        Snap(value.X, cell.X),
        Snap(value.Y, cell.Y));

    /// <summary>A new block is two cells wide and one cell tall, a small platform.</summary>
    public static Vector2 DefaultSize(Playfield playfield)
    {
        Vector2 cell = Cell(playfield);
        return new Vector2(cell.X * 2, cell.Y);
    }

    /// <summary>Top-left of a block dropped at the pointer, snapped and kept in the field.</summary>
    public static Vector2 PlaceAt(Vector2 pointer, Vector2 size, Playfield playfield) =>
        Clamp(Snap(pointer - size / 2, Cell(playfield)), size, playfield);

    /// <summary>Keeps a block fully inside the field without breaking grid alignment.</summary>
    public static Vector2 Clamp(Vector2 position, Vector2 size, Playfield playfield) => new(
        Mathf.Clamp(position.X, 0, Mathf.Max(0, playfield.Width - size.X)),
        Mathf.Clamp(position.Y, 0, Mathf.Max(0, playfield.Height - size.Y)));
}

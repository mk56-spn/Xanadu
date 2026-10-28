using Godot;
using Xanadu.Ecs;

namespace Xanadu.Systems.Playfields;

/// <summary>Geometry for spike tiles: one grid cell each, snapped to the playfield grid.</summary>
public static class SpikeRules
{
    /// <summary>The size of one spike tile on this playfield.</summary>
    public static Vector2 TileSize(Playfield playfield) => PlayfieldGrid.Cell(playfield);

    /// <summary>A spike filling the grid cell under <paramref name="point"/>.</summary>
    public static Spike TileAt(Vector2 point, Playfield playfield)
    {
        Vector2 cell = TileSize(playfield);
        return new Spike(Clamp(PlayfieldGrid.Snap(point, playfield), cell, playfield), cell);
    }

    /// <summary>Moves a spike by <paramref name="delta"/>, snapped to the grid and kept inside the field.</summary>
    public static Spike Move(Spike spike, Vector2 delta, Playfield playfield)
    {
        Vector2 position = PlayfieldGrid.Snap(spike.Position + delta, playfield);
        return new Spike(Clamp(position, spike.Size, playfield), spike.Size);
    }

    /// <summary>The tile's box on screen, for drawing and collision.</summary>
    public static Rect2 ScreenBounds(Spike spike, PlayfieldView view) => new(
        view.PlayfieldToScreen(spike.Position),
        spike.Size * view.Scale);

    /// <summary>Keeps a tile fully inside the field without breaking grid alignment.</summary>
    public static Vector2 Clamp(Vector2 position, Vector2 size, Playfield playfield) => new(
        Mathf.Clamp(position.X, 0, Mathf.Max(0, playfield.Width - size.X)),
        Mathf.Clamp(position.Y, 0, Mathf.Max(0, playfield.Height - size.Y)));
}

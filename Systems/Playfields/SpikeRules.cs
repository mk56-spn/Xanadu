using Godot;
using Xanadu.Ecs;

namespace Xanadu.Systems.Playfields;

/// <summary>A spike's triangle: its tip and the two corners of its base.</summary>
public readonly record struct SpikeTriangle(Vector2 Tip, Vector2 BaseLeft, Vector2 BaseRight)
{
    /// <summary>The same triangle scaled about its centre.</summary>
    public SpikeTriangle Scaled(float factor)
    {
        Vector2 center = (Tip + BaseLeft + BaseRight) / 3f;
        return new SpikeTriangle(
            center + (Tip - center) * factor,
            center + (BaseLeft - center) * factor,
            center + (BaseRight - center) * factor);
    }

    /// <summary>
    /// Whether the triangle overlaps a box, by separating axes: the box's two
    /// and one per triangle edge. Only touching does not count, as with
    /// <see cref="Rect2.Intersects"/>.
    /// </summary>
    public bool Overlaps(Rect2 box) =>
        OverlapsAlong(Vector2.Right, box) &&
        OverlapsAlong(Vector2.Down, box) &&
        OverlapsAlong((BaseLeft - Tip).Orthogonal(), box) &&
        OverlapsAlong((BaseRight - BaseLeft).Orthogonal(), box) &&
        OverlapsAlong((Tip - BaseRight).Orthogonal(), box);

    private bool OverlapsAlong(Vector2 axis, Rect2 box)
    {
        float tip = Tip.Dot(axis);
        float left = BaseLeft.Dot(axis);
        float right = BaseRight.Dot(axis);
        float triangleMin = Mathf.Min(tip, Mathf.Min(left, right));
        float triangleMax = Mathf.Max(tip, Mathf.Max(left, right));

        Vector2 center = box.GetCenter();
        float reach = Mathf.Abs(axis.X) * box.Size.X / 2f +
                      Mathf.Abs(axis.Y) * box.Size.Y / 2f;
        float boxCenter = center.Dot(axis);
        return triangleMin < boxCenter + reach && boxCenter - reach < triangleMax;
    }
}

/// <summary>Geometry for spike tiles: one grid cell each, snapped to the playfield grid.</summary>
public static class SpikeRules
{
    /// <summary>How much of the drawn spike is deadly, so a graze of its edge is forgiven.</summary>
    public const float HitboxScale = 0.8f;

    /// <summary>The size of one spike tile on this playfield.</summary>
    public static Vector2 TileSize(Playfield playfield) => PlayfieldGrid.Cell(playfield);

    /// <summary>A spike filling the grid cell under <paramref name="point"/>.</summary>
    public static Spike TileAt(Vector2 point, Playfield playfield, int quarterTurns = 0)
    {
        Vector2 cell = TileSize(playfield);
        return new Spike(
            Clamp(PlayfieldGrid.Snap(point, playfield), cell, playfield),
            cell,
            quarterTurns);
    }

    /// <summary>Moves a spike by <paramref name="delta"/>, snapped to the grid and kept inside the field.</summary>
    public static Spike Move(Spike spike, Vector2 delta, Playfield playfield)
    {
        Vector2 position = PlayfieldGrid.Snap(spike.Position + delta, playfield);
        return new Spike(Clamp(position, spike.Size, playfield), spike.Size, spike.QuarterTurns);
    }

    /// <summary>The tile's box on screen.</summary>
    public static Rect2 ScreenBounds(Spike spike, PlayfieldView view) => new(
        view.PlayfieldToScreen(spike.Position),
        spike.Size * view.Scale);

    /// <summary>A quarter-turn count brought into 0 to 3.</summary>
    public static int NormalizeTurns(int quarterTurns) => Mathf.PosMod(quarterTurns, 4);

    /// <summary>
    /// The drawn spike: its tip mid-way along the edge it points at, its base
    /// the opposite edge. It stays inside its tile however it is turned.
    /// </summary>
    public static SpikeTriangle ScreenTriangle(Spike spike, PlayfieldView view)
    {
        Rect2 bounds = ScreenBounds(spike, view);
        Vector2 center = bounds.GetCenter();
        Vector2 topLeft = bounds.Position;
        Vector2 bottomRight = bounds.End;
        Vector2 topRight = new(bottomRight.X, topLeft.Y);
        Vector2 bottomLeft = new(topLeft.X, bottomRight.Y);

        return NormalizeTurns(spike.QuarterTurns) switch
        {
            1 => new SpikeTriangle(new Vector2(bottomRight.X, center.Y), topLeft, bottomLeft),
            2 => new SpikeTriangle(new Vector2(center.X, bottomRight.Y), topRight, topLeft),
            3 => new SpikeTriangle(new Vector2(topLeft.X, center.Y), bottomRight, topRight),
            _ => new SpikeTriangle(new Vector2(center.X, topLeft.Y), bottomLeft, bottomRight),
        };
    }

    /// <summary>What kills the player: the drawn spike, slightly smaller.</summary>
    public static SpikeTriangle ScreenHitbox(Spike spike, PlayfieldView view) =>
        ScreenTriangle(spike, view).Scaled(HitboxScale);

    /// <summary>Keeps a tile fully inside the field without breaking grid alignment.</summary>
    public static Vector2 Clamp(Vector2 position, Vector2 size, Playfield playfield) => new(
        Mathf.Clamp(position.X, 0, Mathf.Max(0, playfield.Width - size.X)),
        Mathf.Clamp(position.Y, 0, Mathf.Max(0, playfield.Height - size.Y)));
}

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

/// <summary>A spike's triangle. Where it may be placed is <c>GridPlacement</c>'s business, shared with blocks.</summary>
public static class SpikeRules
{
    /// <summary>How much of the drawn spike is deadly, so a graze of its edge is forgiven.</summary>
    public const float HitboxScale = 0.8f;

    /// <summary>The tile's unturned box on screen.</summary>
    public static Rect2 ScreenBounds(Spike spike, PlayfieldView view) => new(
        view.PlayfieldToScreen(spike.Position),
        spike.Size * view.Scale);

    /// <summary>An angle brought into 0 up to 360 degrees.</summary>
    public static float NormalizeDegrees(float degrees) => Mathf.PosMod(degrees, 360f);

    /// <summary>
    /// The drawn spike: tip mid-way along the box's top edge, base along its
    /// bottom edge, the whole thing turned around the box's centre.
    /// </summary>
    public static SpikeTriangle ScreenTriangle(Spike spike, PlayfieldView view)
    {
        Rect2 bounds = ScreenBounds(spike, view);
        Vector2 center = bounds.GetCenter();
        float angle = Mathf.DegToRad(spike.RotationDegrees);
        Vector2 Turned(Vector2 point) => center + (point - center).Rotated(angle);
        return new SpikeTriangle(
            Turned(new Vector2(center.X, bounds.Position.Y)),
            Turned(new Vector2(bounds.Position.X, bounds.End.Y)),
            Turned(bounds.End));
    }

    /// <summary>What kills the player: the drawn spike, slightly smaller.</summary>
    public static SpikeTriangle ScreenHitbox(Spike spike, PlayfieldView view) =>
        ScreenTriangle(spike, view).Scaled(HitboxScale);
}

using Friflo.Engine.ECS;
using Godot;

namespace Xanadu.Ecs;

/// <summary>Pure polar math for <see cref="UiWheel"/> hit-testing and layout.</summary>
public static class UiWheelGeometry
{
    /// <summary>Wedge zero is centred straight up, matching the dash picker.</summary>
    public const float DefaultStartAngle = -Mathf.Pi / 2;

    /// <summary>
    /// Returns the wedge containing a pointer offset from the wheel centre, or
    /// -1 when there are no wedges. Angles increase clockwise because Godot's
    /// y axis points down.
    /// </summary>
    public static int IndexAt(
        Vector2 offset,
        int count,
        float startAngle = DefaultStartAngle)
    {
        if (count <= 0)
            return -1;

        float step = Mathf.Tau / count;
        float angle = Mathf.Atan2(offset.Y, offset.X);
        float halfStep = step / 2;
        float relative = Normalize(angle - (startAngle - halfStep));
        return Wrap((int)Mathf.Floor(relative / step), count);
    }

    /// <summary>Normalises an angle into [0, tau).</summary>
    public static float Normalize(float angle)
    {
        angle %= Mathf.Tau;
        return angle < 0 ? angle + Mathf.Tau : angle;
    }

    /// <summary>Wraps an option index into [0, count).</summary>
    public static int Wrap(int index, int count)
    {
        if (count <= 0)
            return 0;

        return ((index % count) + count) % count;
    }

    /// <summary>World-space centre of a wheel entity.</summary>
    public static Vector2 Center(Entity wheel)
    {
        if (wheel.IsNull || !wheel.HasComponent<Position>())
            return Vector2.Zero;

        return UiTransform.WorldPosition(
            wheel,
            wheel.GetComponent<Position>().Value);
    }

    /// <summary>Outer radius derived from the wheel entity's square bounds.</summary>
    public static float Radius(Entity wheel)
    {
        if (wheel.IsNull || !wheel.HasComponent<Rectangle>())
            return 0;

        Vector2 size = wheel.GetComponent<Rectangle>().Bounds.Size;
        return Mathf.Min(size.X, size.Y) / 2;
    }

    /// <summary>True when a screen point falls inside the wheel's outer circle.</summary>
    public static bool Contains(Entity wheel, Vector2 screenPoint)
    {
        if (wheel.IsNull || !wheel.HasComponent<UiWheel>())
            return false;

        return screenPoint.DistanceTo(Center(wheel)) <= Radius(wheel);
    }

    /// <summary>
    /// Wedge under a screen point, or -1 when the point is outside the wheel or
    /// inside the hub. <paramref name="allowOutside"/> keeps a drag selecting by
    /// angle after the pointer leaves the rim.
    /// </summary>
    public static int OptionAt(
        Entity wheel,
        Vector2 screenPoint,
        bool allowOutside = false)
    {
        if (wheel.IsNull ||
            !wheel.HasComponent<UiWheel>() ||
            !wheel.HasComponent<Rectangle>())
        {
            return -1;
        }

        UiWheel data = wheel.GetComponent<UiWheel>();
        float radius = Radius(wheel);
        Vector2 offset = screenPoint - Center(wheel);
        float distance = offset.Length();

        if (!allowOutside && distance > radius)
            return -1;

        float hub = radius * Mathf.Clamp(data.HubRadius, 0f, 0.95f);
        if (distance <= Mathf.Max(2f, hub * 0.9f))
            return -1;

        if (data.Labels is not { Length: > 0 })
            return -1;

        return IndexAt(offset, data.Labels.Length, data.StartAngle);
    }

    /// <summary>Screen position of a wedge's label, between the hub and rim.</summary>
    public static Vector2 LabelPosition(
        Entity wheel,
        int index,
        float radialFraction = 0.64f)
    {
        if (wheel.IsNull || !wheel.HasComponent<UiWheel>())
            return Vector2.Zero;

        UiWheel data = wheel.GetComponent<UiWheel>();
        if (data.Labels is not { Length: > 0 })
            return Center(wheel);

        float step = Mathf.Tau / data.Labels.Length;
        float angle = data.StartAngle + Wrap(index, data.Labels.Length) * step;
        return Center(wheel) + new Vector2(
            Mathf.Cos(angle),
            Mathf.Sin(angle)) * (Radius(wheel) * radialFraction);
    }
}

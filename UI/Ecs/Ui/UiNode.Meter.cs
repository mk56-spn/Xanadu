using Friflo.Engine.ECS;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Ecs;

/// <summary>
/// A meter's reading: how full it is, and where it turns from the theme's good
/// colour to its warning one and then its bad one. The track is a row holding
/// the fill and the empty part, which share its width by grow weight, so the
/// meter is laid out like any other row; <see cref="UiMeters.Set"/> changes it.
/// </summary>
public struct UiMeter(float share, float warnAt, float badAt) : IComponent
{
    /// <summary>0 to 1; more than 1 is shown full.</summary>
    public float Share = share;

    public float WarnAt = warnAt;
    public float BadAt = badAt;
}

public readonly partial struct UiNode
{
    /// <summary>
    /// A bar showing <paramref name="share"/> of something filled, in the
    /// theme's good colour, its warning colour from <paramref name="warnAt"/>
    /// and its bad colour from <paramref name="badAt"/>. It has no size of its
    /// own: give it one with <see cref="Size(float, float)"/>, or grow it.
    /// </summary>
    public UiNode Meter(float share, float warnAt = 0.75f, float badAt = 0.9f)
    {
        UiNode track = Row().Fill(Roles.Inset).Cross(StackCrossAlignment.Stretch);
        track.Rect(Roles.Good).Grow(0);
        track.Rect(Transparent).Grow(0);
        track.Entity.AddComponent(new UiMeter(share, warnAt, badAt));
        UiMeters.Set(track, share);
        return track;
    }
}

/// <summary>Changing a meter after it is built.</summary>
public static class UiMeters
{
    /// <summary>Shows <paramref name="share"/> on the meter: the fill's grow weight, the rest's, and the fill's colour.</summary>
    public static void Set(Entity meter, float share)
    {
        if (meter.IsNull || !meter.HasComponent<UiMeter>() || meter.ChildCount < 2)
            return;

        ref UiMeter reading = ref meter.GetComponent<UiMeter>();
        reading.Share = share;
        float full = Mathf.Clamp(share, 0, 1);

        UiNode node = UiNode.Wrap(meter);
        UiThemeRoles roles = node.Roles;
        Color colour = share >= reading.BadAt ? roles.Bad : share >= reading.WarnAt ? roles.Warn : roles.Good;
        UiNode.Wrap(meter.ChildEntities[0]).Grow(full).Fill(colour);
        UiNode.Wrap(meter.ChildEntities[1]).Grow(1 - full);
    }
}

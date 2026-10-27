using Godot;

namespace Xanadu.Ecs;

/// <summary>How a group effect travels from its start to its end.</summary>
public enum GroupEase
{
    /// <summary>A constant speed from start to end.</summary>
    Linear,

    /// <summary>Starts slowly and speeds up.</summary>
    In,

    /// <summary>Starts quickly and slows down.</summary>
    Out,

    /// <summary>Starts and ends slowly, with the fastest part in the middle.</summary>
    InOut,
}

/// <summary>The easing curves, written as plain maths so play and tests share them.</summary>
public static class GroupEaseCurves
{
    /// <summary>The eased fraction of an effect at a linear fraction <paramref name="t"/>.</summary>
    public static float Apply(GroupEase ease, float t)
    {
        t = Mathf.Clamp(t, 0f, 1f);
        return ease switch
        {
            GroupEase.In => t * t,
            GroupEase.Out => 1f - (1f - t) * (1f - t),
            GroupEase.InOut => t * t * (3f - 2f * t),
            _ => t,
        };
    }

    /// <summary>The next curve in the editor's click-to-cycle order.</summary>
    public static GroupEase Next(GroupEase ease) => ease switch
    {
        GroupEase.Linear => GroupEase.In,
        GroupEase.In => GroupEase.Out,
        GroupEase.Out => GroupEase.InOut,
        _ => GroupEase.Linear,
    };
}

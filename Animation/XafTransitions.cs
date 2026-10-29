using System;
using Godot;

namespace Xanadu.Animation;

/// <summary>How a crossfade's weight moves from the old clip to the new one.</summary>
public enum XafFadeCurve : byte
{
    Linear,

    /// <summary>Slow at both ends, so neither clip is left or reached with a jolt.</summary>
    Smooth,

    /// <summary>Holds the old clip longer, then hurries into the new one.</summary>
    EaseIn,

    /// <summary>Leaves the old clip at once, then settles slowly into the new one.</summary>
    EaseOut,
}

/// <summary>
/// One allowed hand-over: the clip <see cref="From"/> may fade into the clip
/// <see cref="To"/>, over <see cref="Fade"/> seconds along <see cref="Curve"/>.
/// </summary>
public sealed class XafTransition
{
    public string From = "";
    public string To = "";
    public float Fade = XafTransitions.DefaultFade;
    public XafFadeCurve Curve = XafFadeCurve.Linear;
}

/// <summary>
/// The package's transition table: which clips may follow which, and how they
/// blend. An empty table is open, so every clip may follow any other with the
/// default fade, which is how files from before the table behave.
/// </summary>
public static class XafTransitions
{
    /// <summary>The fade used where no rule gives one; the game used it for every hand-over before rules.</summary>
    public const float DefaultFade = 0.1f;

    public const float MaxFade = 2f;

    /// <summary>One press of the fade arrows in the animator.</summary>
    public const float FadeStep = 0.05f;

    /// <summary>Whether the table has any rules; with none, every hand-over is allowed.</summary>
    public static bool IsOpen(XafPackage package) => package.Transitions.Count == 0;

    public static XafTransition? Find(XafPackage package, string from, string to) =>
        package.Transitions.Find(rule => rule.From == from && rule.To == to);

    /// <summary>Whether <paramref name="from"/> may fade into <paramref name="to"/>.</summary>
    public static bool Allows(XafPackage package, string from, string to) =>
        from == to || IsOpen(package) || Find(package, from, to) is not null;

    /// <summary>The fade and curve of a hand-over: its rule's, or the default.</summary>
    public static (float Fade, XafFadeCurve Curve) Blend(XafPackage package, string from, string to) =>
        Find(package, from, to) is { } rule
            ? (rule.Fade, rule.Curve)
            : (DefaultFade, XafFadeCurve.Linear);

    /// <summary>The new clip's share of the pose, 0 to 1, at <paramref name="t"/> of the way through the fade.</summary>
    public static float Weight(XafFadeCurve curve, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return curve switch
        {
            XafFadeCurve.Smooth => t * t * (3f - 2f * t),
            XafFadeCurve.EaseIn => t * t,
            XafFadeCurve.EaseOut => 1f - (1f - t) * (1f - t),
            _ => t,
        };
    }

    /// <summary>The rule from one clip to another, added with the default fade when there is none.</summary>
    public static XafTransition Allow(XafPackage package, string from, string to)
    {
        if (Find(package, from, to) is { } existing)
            return existing;

        var rule = new XafTransition { From = from, To = to };
        package.Transitions.Add(rule);
        return rule;
    }

    public static bool Forbid(XafPackage package, string from, string to) =>
        package.Transitions.RemoveAll(rule => rule.From == from && rule.To == to) > 0;

    public static float ClampFade(float seconds) =>
        float.IsFinite(seconds) ? Math.Clamp(seconds, 0f, MaxFade) : DefaultFade;

    /// <summary>Points the rules that name a clip at its new id.</summary>
    public static void RenameClip(XafPackage package, string oldId, string newId)
    {
        foreach (XafTransition rule in package.Transitions)
        {
            if (rule.From == oldId)
                rule.From = newId;
            if (rule.To == oldId)
                rule.To = newId;
        }
    }

    /// <summary>Drops the rules into and out of a clip that is going away.</summary>
    public static void RemoveClip(XafPackage package, string id) =>
        package.Transitions.RemoveAll(rule => rule.From == id || rule.To == id);

    /// <summary>
    /// How far the rig jumps at a hand-over: the bone whose joint is furthest
    /// between the old clip at <paramref name="fromTime"/> and the new clip's
    /// first frame, and that distance in rig units. A big gap needs a longer
    /// fade, or a pose closer to the next clip's start.
    /// </summary>
    public static (float Distance, string BoneId) Gap(
        XafPackage package,
        XafClip from,
        float fromTime,
        XafClip to)
    {
        Transform2D[] a = XafRig.WorldBones(package, XafSampler.Sample(package, from, fromTime));
        Transform2D[] b = XafRig.WorldBones(package, XafSampler.Sample(package, to, 0f));
        float distance = 0f;
        string bone = "";
        for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            float d = a[i].Origin.DistanceTo(b[i].Origin);
            if (d <= distance)
                continue;

            distance = d;
            bone = package.Skeleton.Bones[i].Id;
        }
        return (distance, bone);
    }
}

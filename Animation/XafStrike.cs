using System;

namespace Xanadu.Animation;

/// <summary>How a layered clip combines with the pose under it.</summary>
public enum XafLayerBlend
{
    /// <summary>Its keyed values are added on top, as offsets.</summary>
    Additive,

    /// <summary>What it keys replaces the pose under it; the rest shows through.</summary>
    Override,
}

/// <summary>
/// Plays an attack clip as a hit: layered over the base as an override, from
/// its contact frame rather than its start, so the hit lands the frame the
/// input does. It holds full weight through the follow-through and fades back
/// into the base over the clip's last moment.
/// </summary>
public static class XafStrike
{
    /// <summary>The event names that mark an attack's contact frame.</summary>
    public const string ContactEvent = XafEventAuthoring.DefaultName;
    public const string ContactEventAlias = "contact";

    /// <summary>How long the layer fades back into the base at the clip's end.</summary>
    public const float BlendOutSeconds = 0.12f;

    /// <summary>Where the hit lands in the clip: its first "hit" or "contact" event, else its start.</summary>
    public static float ContactTime(XafClip clip)
    {
        foreach (XafEvent marker in clip.Events)
        {
            if (marker.Name is ContactEvent or ContactEventAlias)
                return Math.Clamp(marker.Time, 0f, clip.Duration);
        }

        return 0f;
    }

    /// <summary>The layer's weight at a time in the clip: full, then fading out over the end.</summary>
    public static float Weight(XafClip clip, float time)
    {
        float span = clip.Duration - ContactTime(clip);
        float fade = Math.Min(BlendOutSeconds, span / 2f);
        if (fade <= 0f)
            return time < clip.Duration ? 1f : 0f;
        return Math.Clamp((clip.Duration - time) / fade, 0f, 1f);
    }

    /// <summary>Layers the attack on from its contact frame, replacing a run of it already playing.</summary>
    public static void Start(AnimatorRuntime runtime, XafClip clip)
    {
        runtime.RemoveLayer(clip.Id);
        float contact = ContactTime(clip);
        runtime.AddLayer(clip.Id, Weight(clip, contact), XafLayerBlend.Override, contact);
    }

    /// <summary>
    /// Sets the playing attack's weight for where it has got to, and takes it
    /// off once it has played out. False when it is no longer playing.
    /// </summary>
    public static bool Step(AnimatorRuntime runtime, XafClip clip)
    {
        if (runtime.Layer(clip.Id) is not { } layer)
            return false;

        if (layer.Time >= clip.Duration)
        {
            runtime.RemoveLayer(clip.Id);
            return false;
        }

        runtime.AddLayer(clip.Id, Weight(clip, layer.Time));
        return true;
    }
}

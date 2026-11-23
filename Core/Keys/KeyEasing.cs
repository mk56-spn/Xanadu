using Xanadu.Core.Timeline;

namespace Xanadu.Core.Keys;

/// <summary>
/// An <see cref="EasePreset"/> on one key of a channel, for every timeline
/// that keys (the animator's clips, the level's groups): set as an ease mode
/// plus the tangents that bound the span to the next key, and read back the
/// same way. Bezier handles can be authored later without changing the format.
/// </summary>
public static class KeyEasing
{
    private const float Tolerance = 0.001f;

    /// <summary>The stored ease of a named-curve preset, or null for the ones drawn with tangents.</summary>
    public static KeyEase? NamedCurve(EasePreset preset) =>
        preset > EasePreset.EaseOut && Enum.TryParse(preset.ToString(), out KeyEase curve)
            ? curve
            : null;

    /// <summary>Sets <paramref name="preset"/> on the channel's key at <paramref name="index"/>.</summary>
    public static void Apply(KeyChannel channel, int index, EasePreset preset)
    {
        Keyframe key = channel.Keys[index];
        key.Ease = preset switch
        {
            EasePreset.Step => KeyEase.Step,
            EasePreset.Linear => KeyEase.Linear,
            _ => NamedCurve(preset) ?? KeyEase.CubicBezier,
        };

        key.TangentOut = 0f;
        if (index + 1 >= channel.Keys.Count)
            return;

        Keyframe next = channel.Keys[index + 1];
        next.TangentIn = 0f;
        if (key.Ease != KeyEase.CubicBezier)
            return;

        float span = next.Time - key.Time;
        float slope = span > 0f ? (next.Value - key.Value) / span : 0f;
        if (preset == EasePreset.EaseIn)
            next.TangentIn = 2f * slope;
        else if (preset == EasePreset.EaseOut)
            key.TangentOut = 2f * slope;
    }

    /// <summary>The preset the channel's key at <paramref name="index"/> leaves with.</summary>
    public static EasePreset Classify(KeyChannel channel, int index)
    {
        Keyframe key = channel.Keys[index];
        if (key.Ease == KeyEase.Step)
            return EasePreset.Step;
        if (key.Ease == KeyEase.Linear)
            return EasePreset.Linear;
        if (key.Ease != KeyEase.CubicBezier &&
            Enum.TryParse(key.Ease.ToString(), out EasePreset named))
        {
            return named;
        }
        if (index + 1 >= channel.Keys.Count)
            return EasePreset.Ease;

        Keyframe next = channel.Keys[index + 1];
        float span = next.Time - key.Time;
        float slope = span > 0f ? (next.Value - key.Value) / span : 0f;
        float tolerance = Tolerance * (1f + Math.Abs(slope));
        bool outFlat = Math.Abs(key.TangentOut) <= tolerance;
        bool inFlat = Math.Abs(next.TangentIn) <= tolerance;
        if (outFlat && !inFlat && Math.Abs(next.TangentIn - 2f * slope) <= tolerance)
            return EasePreset.EaseIn;
        if (inFlat && !outFlat && Math.Abs(key.TangentOut - 2f * slope) <= tolerance)
            return EasePreset.EaseOut;
        return EasePreset.Ease;
    }
}

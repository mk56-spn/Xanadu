using System.Collections.Generic;
using Xanadu.Timeline;

namespace Xanadu.Ecs;

/// <summary>
/// Works out what a shader track's keys say at a moment
/// (docs/render-architecture.md step 8). A key holds the value from its time
/// until the next key for the same setting eases into its own; before the
/// first key the first one's value already applies, and after the last it
/// holds. Pure, so the editor, play and tests share it.
/// </summary>
public static class ShaderKeyTrack
{
    /// <summary>
    /// The value of one setting at <paramref name="seconds"/>, or
    /// <paramref name="unkeyed"/> (the shader's declared default) when the
    /// track has no key for it.
    /// </summary>
    public static Vector4 ValueAt(
        IReadOnlyList<ShaderKey> keys,
        string param,
        float seconds,
        Vector4 unkeyed = default)
    {
        ShaderKey? before = null;
        ShaderKey? after = null;
        foreach (ShaderKey key in keys)
        {
            if (key.Param != param)
                continue;

            if (key.Time <= seconds)
            {
                if (before is null || key.Time >= before.Value.Time)
                    before = key;
            }
            else if (after is null || key.Time < after.Value.Time)
            {
                after = key;
            }
        }

        if (before is null)
            return after?.Value ?? unkeyed;
        if (after is null)
            return before.Value.Value;

        float span = after.Value.Time - before.Value.Time;
        if (span <= 0f)
            return after.Value.Value;

        float t = EasePresets.Progress(before.Value.Ease, (seconds - before.Value.Time) / span);
        return before.Value.Value + (after.Value.Value - before.Value.Value) * t;
    }

    /// <summary>
    /// Every setting the track keys, in the order it first appears, with its
    /// value at <paramref name="seconds"/> and the size it was stored with.
    /// </summary>
    public static List<(string Param, Vector4 Value, int Size)> Evaluate(
        IReadOnlyList<ShaderKey> keys,
        float seconds)
    {
        var values = new List<(string Param, Vector4 Value, int Size)>();
        foreach (ShaderKey key in keys)
        {
            bool known = false;
            foreach ((string param, Vector4 _, int _) in values)
            {
                if (param == key.Param)
                {
                    known = true;
                    break;
                }
            }

            if (!known)
                values.Add((key.Param, ValueAt(keys, key.Param, seconds), key.Size));
        }

        return values;
    }
}

using System.Collections.Generic;
using Xanadu.Core.Timeline;

namespace Xanadu.Core.Keys;

/// <summary>One key of a channel: a value at a time, and how it travels to the next key.</summary>
public sealed class Keyframe
{
    public float Time;
    public float Value;
    public KeyEase Ease = KeyEase.Linear;

    /// <summary>The cubic curve's slopes either side of the key; zero until bezier authoring exists.</summary>
    public float TangentIn;
    public float TangentOut;

    public Keyframe Copy() => new() { Time = Time, Value = Value, Ease = Ease, TangentIn = TangentIn, TangentOut = TangentOut };

    public bool Same(Keyframe other) =>
        Time == other.Time && Value == other.Value && Ease == other.Ease &&
        TangentIn == other.TangentIn && TangentOut == other.TangentOut;
}

/// <summary>
/// What a channel keys: a field of a node. <see cref="Node"/> is the node's id,
/// relative to the Xif the clip animates ("" is that Xif itself);
/// <see cref="Part"/> names an element inside one of its modules, such as a
/// bone of a skeleton, or "" for the node's own fields; <see cref="Field"/>
/// is a field its modules declare (<see cref="IKeyable"/>).
/// </summary>
public readonly record struct KeyAddress(string Node, string Part, string Field)
{
    /// <summary>The same thing keyed, whatever field: node and part.</summary>
    public bool SameTarget(KeyAddress other) => Node == other.Node && Part == other.Part;
}

/// <summary>One keyed field: its address and its keys, in time order.</summary>
public sealed class KeyChannel
{
    /// <summary>Keys closer than this are at one time.</summary>
    public const float TimeTolerance = 0.0001f;

    public KeyAddress Address = new("", "", "");
    public List<Keyframe> Keys = [];

    public KeyChannel Copy()
    {
        var copy = new KeyChannel { Address = Address };
        foreach (Keyframe key in Keys)
            copy.Keys.Add(key.Copy());
        return copy;
    }

    /// <summary>The value at <paramref name="time"/>, eased between the keys either side; 0 with no keys.</summary>
    public float Sample(float time)
    {
        if (Keys.Count == 0)
            return 0f;
        if (Keys.Count == 1 || time <= Keys[0].Time)
            return Keys[0].Value;

        for (int i = 1; i < Keys.Count; i++)
        {
            Keyframe next = Keys[i];
            if (time > next.Time)
                continue;

            Keyframe previous = Keys[i - 1];
            float span = next.Time - previous.Time;
            if (span <= 0f)
                return next.Value;

            float t = (time - previous.Time) / span;
            return previous.Ease switch
            {
                // At the next key's own time the value is that key's.
                KeyEase.Step => t >= 1f ? next.Value : previous.Value,
                KeyEase.CubicBezier => EaseCurves.Hermite(previous.Value, next.Value, previous.TangentOut, next.TangentIn, t, span),
                KeyEase.Linear => float.Lerp(previous.Value, next.Value, t),
                _ => KeyEases.Shape(previous.Ease) is var (family, direction)
                    ? float.Lerp(previous.Value, next.Value, EaseCurves.Evaluate(family, direction, t))
                    : float.Lerp(previous.Value, next.Value, t),
            };
        }

        return Keys[^1].Value;
    }

    /// <summary>
    /// A held value: the latest key at or before the time, so a switch flips
    /// exactly where its key sits rather than half way to the next. Before the
    /// first key it is <paramref name="before"/>.
    /// </summary>
    public float SampleHeld(float time, float before = 0f)
    {
        float value = before;
        foreach (Keyframe key in Keys)
        {
            if (key.Time > time + TimeTolerance)
                break;
            value = key.Value;
        }
        return value;
    }
}

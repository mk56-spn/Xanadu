using System.Collections.Generic;

namespace Xanadu.Core.Keys;

/// <summary>What a row of keys stands for: a node, or a part inside one ("" for the node's own fields).</summary>
public readonly record struct KeyTarget(string Node, string Part)
{
    public static KeyTarget Of(KeyAddress address) => new(address.Node, address.Part);

    public KeyAddress Field(string field) => new(Node, Part, field);

    public bool Holds(KeyAddress address) => address.Node == Node && address.Part == Part;
}

/// <summary>
/// The reading every key timeline shares, whatever its clip animates: which
/// targets have keys, which of their fields, and at what times. A target row
/// merges its fields' keys; a field row shows one channel's. Fields come in the
/// order their module declares them (<see cref="IKeyable"/>).
/// </summary>
public static class KeyRows
{
    /// <summary>Every target with at least one key, in the order the clip first keys them.</summary>
    public static List<KeyTarget> Animated(KeyClip clip)
    {
        var targets = new List<KeyTarget>();
        var seen = new HashSet<KeyTarget>();
        foreach (KeyChannel channel in clip.Channels)
        {
            var target = KeyTarget.Of(channel.Address);
            if (channel.Keys.Count > 0 && seen.Add(target))
                targets.Add(target);
        }
        return targets;
    }

    /// <summary>The declared fields of <paramref name="target"/> that have keys, in declared order.</summary>
    public static List<KeyField> Fields(KeyClip clip, KeyTarget target, IReadOnlyList<KeyField> declared)
    {
        var fields = new List<KeyField>();
        foreach (KeyField field in declared)
        {
            if (clip.Find(target.Field(field.Name)) is { Keys.Count: > 0 })
                fields.Add(field);
        }
        return fields;
    }

    /// <summary>
    /// The distinct times, ascending, at which <paramref name="target"/> has a key:
    /// in any field, or only in <paramref name="field"/> when one is given.
    /// </summary>
    public static List<float> KeyTimes(KeyClip clip, KeyTarget target, string? field = null)
    {
        var times = new List<float>();
        foreach (KeyChannel channel in clip.Channels)
        {
            if (!target.Holds(channel.Address) || (field is not null && channel.Address.Field != field))
                continue;
            foreach (Keyframe key in channel.Keys)
                times.Add(key.Time);
        }

        times.Sort();
        var distinct = new List<float>();
        foreach (float time in times)
        {
            if (distinct.Count == 0 || time - distinct[^1] > KeyChannel.TimeTolerance)
                distinct.Add(time);
        }
        return distinct;
    }
}

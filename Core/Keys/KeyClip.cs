using System.Collections.Generic;

namespace Xanadu.Core.Keys;

/// <summary>Whose time a clip runs on.</summary>
public enum KeyClock : byte
{
    /// <summary>The clip's own seconds from its start, as a rig clip plays.</summary>
    Clip,

    /// <summary>
    /// The song's beats (from the song's tempo map), as a level group plays:
    /// keys stay on the beat when the song's timing is edited, as notes do.
    /// </summary>
    Song,
}

/// <summary>
/// Keys over time for the fields of a Xif and the nodes under it: the one
/// shape every timeline edits. A rig clip is one on its own clock; a level
/// group will be one on the song's, over the nodes it holds.
/// </summary>
public class KeyClip
{
    public string Id = "";
    public KeyClock Clock = KeyClock.Clip;
    public float Duration;
    public bool Loop;
    public float LoopStart;
    public float LoopEnd;
    public List<KeyChannel> Channels = [];

    /// <summary>The channel keying <paramref name="address"/>, or null.</summary>
    public KeyChannel? Find(KeyAddress address) => Channels.Find(channel => channel.Address == address);

    /// <summary>The channel keying <paramref name="address"/>, added empty when there is none.</summary>
    public KeyChannel Need(KeyAddress address)
    {
        if (Find(address) is { } found)
            return found;
        var channel = new KeyChannel { Address = address };
        Channels.Add(channel);
        return channel;
    }

    /// <summary>
    /// Keys <paramref name="address"/> to <paramref name="value"/> at
    /// <paramref name="time"/>: a key already at that time takes the value, else
    /// a new one goes in, keys staying in time order.
    /// </summary>
    public void SetKey(KeyAddress address, float time, float value)
    {
        List<Keyframe> keys = Need(address).Keys;
        int at = 0;
        while (at < keys.Count && keys[at].Time < time - KeyChannel.TimeTolerance)
            at++;
        if (at < keys.Count && System.Math.Abs(keys[at].Time - time) <= KeyChannel.TimeTolerance)
            keys[at].Value = value;
        else
            keys.Insert(at, new Keyframe { Time = time, Value = value });
    }

    /// <summary>Every distinct time a channel has a key at, ascending: any channel, or only those keying <paramref name="node"/>.</summary>
    public List<float> KeyTimes(string? node = null)
    {
        var times = new List<float>();
        foreach (KeyChannel channel in Channels)
        {
            if (node is not null && channel.Address.Node != node)
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

    /// <summary>Takes out every key at <paramref name="time"/>, in any channel or those keying <paramref name="node"/>; true when any went.</summary>
    public bool RemoveKeysAt(float time, string? node = null)
    {
        int removed = 0;
        foreach (KeyChannel channel in Channels)
            if (node is null || channel.Address.Node == node)
                removed += channel.Keys.RemoveAll(key => System.Math.Abs(key.Time - time) <= KeyChannel.TimeTolerance);
        return removed > 0;
    }

    /// <summary>
    /// Moves every key at <paramref name="from"/> to <paramref name="to"/>, in
    /// every channel; a key a channel already had at <paramref name="to"/> gives
    /// way. Only channels keying <paramref name="node"/> when one is given, and
    /// only <paramref name="field"/>'s when that is. True when anything moved.
    /// </summary>
    public bool MoveKeys(float from, float to, string? node = null, string? field = null)
    {
        if (System.Math.Abs(from - to) <= KeyChannel.TimeTolerance)
            return false;

        bool moved = false;
        foreach (KeyChannel channel in Channels)
        {
            if ((node is not null && channel.Address.Node != node) || (field is not null && channel.Address.Field != field))
                continue;
            Keyframe? key = channel.Keys.Find(k => System.Math.Abs(k.Time - from) <= KeyChannel.TimeTolerance);
            if (key is null)
                continue;

            channel.Keys.RemoveAll(k => k != key && System.Math.Abs(k.Time - to) <= KeyChannel.TimeTolerance);
            key.Time = to;
            channel.Keys.Sort(static (a, b) => a.Time.CompareTo(b.Time));
            moved = true;
        }
        return moved;
    }

    /// <summary>
    /// Sets <paramref name="preset"/> on every key at <paramref name="time"/>, in
    /// any channel or those keying <paramref name="node"/>; true when any key took it.
    /// </summary>
    public bool ApplyEase(float time, Xanadu.Core.Timeline.EasePreset preset, string? node = null)
    {
        bool changed = false;
        foreach (KeyChannel channel in Channels)
        {
            if (node is not null && channel.Address.Node != node)
                continue;
            int index = channel.Keys.FindIndex(key => System.Math.Abs(key.Time - time) <= KeyChannel.TimeTolerance);
            if (index < 0)
                continue;
            KeyEasing.Apply(channel, index, preset);
            changed = true;
        }
        return changed;
    }

    /// <summary>The ease the first key found at <paramref name="time"/> leaves with (any channel, or those keying <paramref name="node"/>), or null with no key there.</summary>
    public Xanadu.Core.Timeline.EasePreset? ReadEase(float time, string? node = null)
    {
        foreach (KeyChannel channel in Channels)
        {
            if (node is not null && channel.Address.Node != node)
                continue;
            int index = channel.Keys.FindIndex(key => System.Math.Abs(key.Time - time) <= KeyChannel.TimeTolerance);
            if (index >= 0)
                return KeyEasing.Classify(channel, index);
        }
        return null;
    }

    /// <summary>Whether two clips key the same things the same way, channel by channel.</summary>
    public bool Same(KeyClip other)
    {
        if (Id != other.Id || Clock != other.Clock || Duration != other.Duration || Loop != other.Loop ||
            LoopStart != other.LoopStart || LoopEnd != other.LoopEnd || Channels.Count != other.Channels.Count)
            return false;

        for (int c = 0; c < Channels.Count; c++)
        {
            KeyChannel a = Channels[c], b = other.Channels[c];
            if (a.Address != b.Address || a.Keys.Count != b.Keys.Count)
                return false;
            for (int k = 0; k < a.Keys.Count; k++)
            {
                if (!a.Keys[k].Same(b.Keys[k]))
                    return false;
            }
        }
        return true;
    }

    /// <summary>A time on the clip, wrapped into its loop when it loops.</summary>
    public float Wrap(float time)
    {
        if (!Loop)
            return time;
        float length = System.Math.Max(0.0001f, LoopEnd - LoopStart);
        float into = (time - LoopStart) % length;
        return LoopStart + (into < 0f ? into + length : into);
    }
}

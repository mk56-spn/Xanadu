using System.Collections.Generic;
using System.Linq;
using Xanadu.Core.Keys;

namespace Xanadu.Model.Levels;

/// <summary>
/// A level group: the placed items it holds, by their ids, and a clip on the
/// song's beats keying their fields. A channel addressed to node "" keys every
/// member at once (a group fade, a group move); one addressed to a member's
/// id keys only that member. What it keys lays over each item's own values
/// (<see cref="LevelItemFields"/>), never into them.
/// </summary>
public sealed class GroupDocument
{
    public string Name = "group";

    /// <summary>The ids of the items the group holds (their Xif ids).</summary>
    public List<string> Members = new();

    public KeyClip Clip = new() { Clock = KeyClock.Song };

    public GroupDocument Copy()
    {
        var copy = new GroupDocument { Name = Name, Members = new List<string>(Members) };
        copy.Clip.Id = Clip.Id;
        copy.Clip.Clock = Clip.Clock;
        copy.Clip.Duration = Clip.Duration;
        copy.Clip.Loop = Clip.Loop;
        copy.Clip.LoopStart = Clip.LoopStart;
        copy.Clip.LoopEnd = Clip.LoopEnd;
        foreach (KeyChannel channel in Clip.Channels)
            copy.Clip.Channels.Add(channel.Copy());
        return copy;
    }

    /// <summary>Whether two groups are alike: name, members in order, and keys.</summary>
    public bool Same(GroupDocument other) =>
        Name == other.Name && Members.SequenceEqual(other.Members) && Clip.Same(other.Clip);

    /// <summary>Where a song beat lands on the group's clip: wrapped into its loop when it loops.</summary>
    public float ClipTime(double beat) => Clip.Wrap((float)beat);

    /// <summary>
    /// What the group's keys for <paramref name="node"/> lay on
    /// <paramref name="field"/> at <paramref name="beat"/>: node "" is every
    /// member at once (the usual way), a member's id only that member's own
    /// keys. The field's neutral value where nothing is keyed.
    /// </summary>
    public float ValueAt(string node, string field, double beat) =>
        Clip.Find(new KeyAddress(node, "", field)) is { Keys.Count: > 0 } channel
            ? channel.Sample(ClipTime(beat))
            : LevelItemFields.Neutral(field);

    /// <summary>Keys <paramref name="field"/> for <paramref name="node"/> ("" = every member) at <paramref name="beat"/>, wrapped into the loop.</summary>
    public void Key(string node, string field, double beat, float value) =>
        Clip.SetKey(new KeyAddress(node, "", field), ClipTime(beat), value);

    /// <summary>
    /// Lets go of items that are gone: out of the members, and their own keys
    /// with them. True when anything changed.
    /// </summary>
    public bool Forget(IReadOnlySet<string> gone)
    {
        int members = Members.RemoveAll(gone.Contains);
        int channels = Clip.Channels.RemoveAll(channel => channel.Address.Node.Length > 0 && gone.Contains(channel.Address.Node));
        return members + channels > 0;
    }

    /// <summary>Lets go of every member and member key whose item isn't among <paramref name="present"/>.</summary>
    public void KeepOnly(IReadOnlySet<string> present)
    {
        Members.RemoveAll(id => !present.Contains(id));
        Clip.Channels.RemoveAll(channel => channel.Address.Node.Length > 0 && !present.Contains(channel.Address.Node));
    }

    /// <summary>Whether any member has keys of its own, not the group's.</summary>
    public bool HasMemberKeys => Clip.Channels.Exists(channel => channel.Address.Node.Length > 0 && channel.Keys.Count > 0);
}

/// <summary>What the groups lay over one placed item at a moment: a move, a turn, a size and a fade.</summary>
public readonly record struct GroupLayer(Vector2 Offset, float Rotation, float Scale, float Opacity)
{
    public static GroupLayer None => new(Vector2.Zero, 0f, 1f, 1f);
}

/// <summary>
/// The fields every placed item has for groups to key, whatever its Xif:
/// where it is (added to its own place), how it is turned and sized, and how
/// see-through (multiplied).
/// </summary>
public static class LevelItemFields
{
    public static readonly KeyField[] All =
    [
        new("x", KeyValueKind.Number, KeyCombine.Add, "move x"),
        new("y", KeyValueKind.Number, KeyCombine.Add, "move y"),
        new("rotation", KeyValueKind.Angle, KeyCombine.Add, "turn"),
        new("scale", KeyValueKind.Number, KeyCombine.Multiply, "size"),
        new("opacity", KeyValueKind.Number, KeyCombine.Multiply, "alpha"),
    ];

    /// <summary>The value that changes nothing: no move or turn, full size and full opacity.</summary>
    public static float Neutral(string field) => field is "scale" or "opacity" ? 1f : 0f;

    /// <summary>
    /// What every group lays over each of its members at <paramref name="beat"/>,
    /// into <paramref name="layers"/> by item id; items in no group are left
    /// out. Moves and turns add up across groups, sizes and fades multiply.
    /// </summary>
    public static void Evaluate(IReadOnlyList<GroupDocument> groups, double beat, Dictionary<string, GroupLayer> layers)
    {
        layers.Clear();
        foreach (GroupDocument group in groups)
        {
            float time = group.Clip.Wrap((float)beat);
            foreach (KeyChannel channel in group.Clip.Channels)
            {
                if (channel.Keys.Count == 0)
                    continue;
                float value = channel.Sample(time);
                if (channel.Address.Node.Length == 0)
                {
                    foreach (string member in group.Members)
                        Lay(layers, member, channel.Address.Field, value);
                }
                else if (group.Members.Contains(channel.Address.Node))
                {
                    Lay(layers, channel.Address.Node, channel.Address.Field, value);
                }
            }
        }
    }

    private static void Lay(Dictionary<string, GroupLayer> layers, string item, string field, float value)
    {
        GroupLayer layer = layers.TryGetValue(item, out GroupLayer known) ? known : GroupLayer.None;
        layers[item] = field switch
        {
            "x" => layer with { Offset = layer.Offset + new Vector2(value, 0f) },
            "y" => layer with { Offset = layer.Offset + new Vector2(0f, value) },
            "rotation" => layer with { Rotation = layer.Rotation + value },
            "scale" => layer with { Scale = layer.Scale * value },
            "opacity" => layer with { Opacity = layer.Opacity * value },
            _ => layer,
        };
    }
}

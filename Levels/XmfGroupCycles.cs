using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xanadu.Ecs;

namespace Xanadu.Levels;

/// <summary>
/// Repeating group effects in an XMF file: for each effect that cycles, its
/// group, its place in the group's list, the repeat count, the beats between
/// cycles and how each cycle plays. Left out when nothing cycles, so files
/// without cycles read and write as before.
/// </summary>
internal static class XmfGroupCycles
{
    private const uint MaxEntries = 4096;

    internal static bool HasCycles(IChartItems items) =>
        items.Groups.Any(group => group.Effects.Any(effect => effect.Cycles > 1));

    internal static byte[] Write(IChartItems items)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        Write(writer, items);
        writer.Flush();
        return stream.ToArray();
    }

    internal static void Write(BinaryWriter writer, IChartItems items)
    {
        var entries = new List<(int Group, int Effect, GroupEffectDocument Document)>();
        foreach (GroupDocument group in items.Groups.OrderBy(group => group.Id))
        {
            for (int i = 0; i < group.Effects.Count; i++)
            {
                if (group.Effects[i].Cycles > 1)
                    entries.Add((group.Id, i, group.Effects[i]));
            }
        }

        writer.Write((uint)entries.Count);
        foreach ((int group, int effect, GroupEffectDocument document) in entries)
        {
            writer.Write(group);
            writer.Write(effect);
            writer.Write(document.Cycles);
            writer.Write(document.EveryBeats);
            writer.Write((byte)document.Cycle);
        }
    }

    internal static void Read(BinaryReader reader, IChartItems items)
    {
        uint count = XmfBinary.ReadCount(reader, MaxEntries, "group cycle");
        for (uint i = 0; i < count; i++)
        {
            int groupId = reader.ReadInt32();
            int index = reader.ReadInt32();
            int cycles = reader.ReadInt32();
            float every = reader.ReadSingle();
            byte mode = reader.ReadByte();
            if (!Enum.IsDefined(typeof(GroupCycle), (int)mode))
                throw new FormatException($"XMF group cycle mode {mode} is not a cycle mode.");

            GroupDocument? group = items.Groups.FirstOrDefault(candidate => candidate.Id == groupId);
            if (group is null || index < 0 || index >= group.Effects.Count)
                continue;

            GroupEffectDocument effect = group.Effects[index];
            effect.Cycles = Math.Clamp(cycles, 1, GroupEffect.MaxCycles);
            effect.EveryBeats = every > 0f ? every : 1f;
            effect.Cycle = (GroupCycle)mode;
        }
    }
}

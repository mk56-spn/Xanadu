using System;
using System.Collections.Generic;
using System.IO;
using Xanadu.Ecs;
using Xanadu.Items;
using Xanadu.Settings;
using Xanadu.Storage;

namespace Xanadu.Levels;

/// <summary>The start-point, group and shader chunks of an XMF file.</summary>
internal static class XmfChromeChunks
{
    internal static byte[] WriteStartPoints(List<StartPointDocument> starts, StringTable strings) =>
        XmfItemIO.WriteList(starts, (writer, start) => WriteStartPoint(writer, start, strings));

    internal static List<StartPointDocument> ReadStartPoints(Chunk chunk, StringTable strings) =>
        XmfItemIO.ReadList(chunk, XmfItemIO.MaxItems, "start point",
            reader => ReadStartPoint(reader, strings));

    private static void WriteStartPoint(BinaryWriter writer, StartPointDocument start, StringTable strings)
    {
        XmfBinary.WriteGroups(writer, start.Groups);
        strings.WriteIndex(writer, start.Name);
        writer.Write(start.TimeMs);
        writer.Write(start.X);
        writer.Write(start.Y);
        writer.Write(start.MomentumX);
        writer.Write(start.MomentumY);
        writer.Write(start.DirectionX);
        writer.Write(start.DirectionY);
    }

    private static StartPointDocument ReadStartPoint(BinaryReader reader, StringTable strings) =>
        new()
        {
            Groups = XmfBinary.ReadGroups(reader),
            Name = strings.ReadIndex(reader),
            TimeMs = reader.ReadInt64(),
            X = reader.ReadSingle(),
            Y = reader.ReadSingle(),
            MomentumX = reader.ReadSingle(),
            MomentumY = reader.ReadSingle(),
            DirectionX = reader.ReadSingle(),
            DirectionY = reader.ReadSingle(),
        };

    internal static byte[] WriteGroupDefinitions(List<GroupDocument> groups, StringTable strings) =>
        XmfItemIO.WriteList(groups, (writer, group) => WriteGroup(writer, group, strings));

    internal static List<GroupDocument> ReadGroupDefinitions(Chunk chunk, StringTable strings) =>
        XmfItemIO.ReadList(chunk, GroupLimits.Count, "group",
            reader => ReadGroup(reader, strings));

    private static void WriteGroup(BinaryWriter writer, GroupDocument group, StringTable strings)
    {
        writer.Write(group.Id);
        strings.WriteIndex(writer, group.Name);
        writer.Write((uint)group.Effects.Count);
        foreach (GroupEffectDocument effect in group.Effects)
        {
            writer.Write((byte)effect.Kind);
            writer.Write(effect.Start);
            writer.Write(effect.End);
            writer.Write((byte)effect.Ease);
            writer.Write(effect.Muted);
            writer.Write(effect.X);
            writer.Write(effect.Y);
            strings.WriteIndex(writer, effect.Tint);
        }
    }

    private static GroupDocument ReadGroup(BinaryReader reader, StringTable strings)
    {
        int id = reader.ReadInt32();
        if (id is < 0 or >= GroupLimits.Count)
            throw new FormatException($"XMF group slot {id} is out of range.");

        var group = new GroupDocument
        {
            Id = id,
            Name = strings.ReadIndex(reader),
        };

        uint effectCount = XmfBinary.ReadCount(reader, XmfItemIO.MaxEffects, "group effect");
        for (uint e = 0; e < effectCount; e++)
        {
            group.Effects.Add(new GroupEffectDocument
            {
                Kind = XmfBinary.ReadEnum<GroupEffectKind>(reader),
                Start = reader.ReadSingle(),
                End = reader.ReadSingle(),
                Ease = XmfBinary.ReadEnum<GroupEase>(reader),
                Muted = reader.ReadBoolean(),
                X = reader.ReadSingle(),
                Y = reader.ReadSingle(),
                Tint = strings.ReadIndex(reader),
            });
        }
        return group;
    }

    internal static byte[] WriteShaders(List<ShaderDocument> shaders, StringTable strings) =>
        XmfItemIO.WriteList(shaders, (writer, shader) => WriteShader(writer, shader, strings));

    internal static List<ShaderDocument> ReadShaders(Chunk chunk, StringTable strings) =>
        XmfItemIO.ReadList(chunk, ShaderLimits.Count, "shader",
            reader => ReadShader(reader, strings));

    private static void WriteShader(BinaryWriter writer, ShaderDocument shader, StringTable strings)
    {
        writer.Write(shader.Slot);
        strings.WriteIndex(writer, shader.Name);
        strings.WriteIndex(writer, shader.Source);
        writer.Write((uint)shader.Params.Count);
        foreach (KeyValuePair<string, float[]> param in shader.Params)
        {
            strings.WriteIndex(writer, param.Key);
            writer.Write((uint)param.Value.Length);
            foreach (float channel in param.Value)
                writer.Write(channel);
        }
    }

    private static ShaderDocument ReadShader(BinaryReader reader, StringTable strings)
    {
        int slot = reader.ReadInt32();
        if (!LevelShaders.IsSlot(slot))
            throw new FormatException($"XMF shader slot {slot} is out of range.");

        var shader = new ShaderDocument
        {
            Slot = slot,
            Name = strings.ReadIndex(reader),
            Source = strings.ReadIndex(reader),
        };

        uint paramCount = XmfBinary.ReadCount(reader, XmfItemIO.MaxParams, "shader parameter");
        for (uint p = 0; p < paramCount; p++)
        {
            string name = strings.ReadIndex(reader);
            uint channels = XmfBinary.ReadCount(reader, XmfItemIO.MaxChannels, "shader channel");
            var values = new float[(int)channels];
            for (uint c = 0; c < channels; c++)
                values[c] = reader.ReadSingle();
            shader.Params[name] = values;
        }
        return shader;
    }
}

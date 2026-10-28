using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xanadu.Ecs;
using Xanadu.Items;
using Xanadu.Settings;
using Xanadu.Storage;

namespace Xanadu.Levels;

public static partial class XmfBinary
{
    private const uint MaxItems = 1_000_000;
    private const uint MaxEffects = 100_000;
    private const uint MaxParams = 4_096;
    private const uint MaxChannels = 16;

    private static byte[] WriteNotes(List<NoteDocument> notes, StringTable strings)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)notes.Count);
        foreach (NoteDocument note in notes)
        {
            WriteGroups(writer, note.Groups);
            WriteItem(writer, note.Item, strings);
            writer.Write(note.TimeMs);
            writer.Write(note.X);
            writer.Write(note.Y);
            writer.Write((byte)note.Target);
            writer.Write((byte)note.Kind);
            writer.Write((byte)note.DashDirection);
            writer.Write(note.ContactRequired);
            writer.Write((byte)note.Character);
        }
        writer.Flush();
        return stream.ToArray();
    }

    private static List<NoteDocument> ReadNotes(Chunk chunk, StringTable strings)
    {
        using BinaryReader reader = chunk.OpenReader();
        uint count = ReadCount(reader, MaxItems, "note");
        var notes = new List<NoteDocument>((int)count);
        for (uint i = 0; i < count; i++)
        {
            notes.Add(new NoteDocument
            {
                Groups = ReadGroups(reader),
                Item = ReadItem(reader, strings),
                TimeMs = reader.ReadInt64(),
                X = reader.ReadSingle(),
                Y = reader.ReadSingle(),
                Target = ReadEnum<KeybindAction>(reader),
                Kind = ReadEnum<NoteKind>(reader),
                DashDirection = ReadEnum<DashDirection>(reader),
                ContactRequired = reader.ReadInt32(),
                Character = ReadEnum<CharacterKind>(reader),
            });
        }
        return notes;
    }

    /// <summary>Every string a Xif's chunk entry carries, so it ends up in the shared table.</summary>
    private static void IndexItemStrings(Xif item, StringTable strings)
    {
        strings.Add(item.Id);
        strings.Add(item.Name);
        foreach (XifPrimitive primitive in item.Primitives)
        {
            strings.Add(primitive.Kind);
            strings.Add(primitive.Ref);
            strings.Add(primitive.Shader.Path);
        }
        foreach (string key in item.Params.Keys)
            strings.Add(key);
        foreach (XifShaderRef shader in item.Shaders)
            strings.Add(shader.Path);
    }

    private static void WriteShaderRef(BinaryWriter writer, XifShaderRef shader, StringTable strings)
    {
        writer.Write((byte)shader.Scope);
        writer.Write(shader.Slot);
        strings.WriteIndex(writer, shader.Path);
    }

    private static XifShaderRef ReadShaderRef(BinaryReader reader, StringTable strings) => new()
    {
        Scope = ReadEnum<XifShaderScope>(reader),
        Slot = reader.ReadInt32(),
        Path = strings.ReadIndex(reader),
    };

    private static void WriteItem(BinaryWriter writer, Xif item, StringTable strings)
    {
        strings.WriteIndex(writer, item.Id);
        strings.WriteIndex(writer, item.Name);
        writer.Write((byte)item.Owner);

        writer.Write((uint)item.Primitives.Count);
        foreach (XifPrimitive primitive in item.Primitives)
        {
            strings.WriteIndex(writer, primitive.Kind);
            strings.WriteIndex(writer, primitive.Ref);
            WriteShaderRef(writer, primitive.Shader, strings);
        }

        writer.Write((uint)item.Shaders.Count);
        foreach (XifShaderRef shader in item.Shaders)
            WriteShaderRef(writer, shader, strings);

        writer.Write((uint)item.Params.Count);
        foreach (KeyValuePair<string, float[]> param in item.Params)
        {
            strings.WriteIndex(writer, param.Key);
            writer.Write((uint)param.Value.Length);
            foreach (float channel in param.Value)
                writer.Write(channel);
        }
    }

    private static Xif ReadItem(BinaryReader reader, StringTable strings)
    {
        var item = new Xif
        {
            Id = strings.ReadIndex(reader),
            Name = strings.ReadIndex(reader),
            Owner = ReadEnum<XifOwner>(reader),
        };

        uint primitiveCount = ReadCount(reader, MaxItems, "xif primitive");
        for (uint i = 0; i < primitiveCount; i++)
        {
            item.Primitives.Add(new XifPrimitive
            {
                Kind = strings.ReadIndex(reader),
                Ref = strings.ReadIndex(reader),
                Shader = ReadShaderRef(reader, strings),
            });
        }

        uint shaderCount = ReadCount(reader, MaxParams, "xif shader");
        for (uint i = 0; i < shaderCount; i++)
            item.Shaders.Add(ReadShaderRef(reader, strings));

        uint paramCount = ReadCount(reader, MaxParams, "xif param");
        for (uint i = 0; i < paramCount; i++)
        {
            string key = strings.ReadIndex(reader);
            uint channelCount = ReadCount(reader, MaxChannels, "xif param channel");
            var channels = new float[channelCount];
            for (uint c = 0; c < channelCount; c++)
                channels[c] = reader.ReadSingle();
            item.Params[key] = channels;
        }

        return item;
    }

    private static byte[] WriteMines(List<MineDocument> mines)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)mines.Count);
        foreach (MineDocument mine in mines)
        {
            WriteGroups(writer, mine.Groups);
            writer.Write(mine.Shader);
            writer.Write(mine.TimeMs);
            writer.Write(mine.X);
            writer.Write(mine.Y);
            writer.Write(mine.FadeInMs);
            writer.Write(mine.FadeOutMs);
        }
        writer.Flush();
        return stream.ToArray();
    }

    private static List<MineDocument> ReadMines(Chunk chunk)
    {
        using BinaryReader reader = chunk.OpenReader();
        uint count = ReadCount(reader, MaxItems, "mine");
        var mines = new List<MineDocument>((int)count);
        for (uint i = 0; i < count; i++)
        {
            mines.Add(new MineDocument
            {
                Groups = ReadGroups(reader),
                Shader = reader.ReadInt32(),
                TimeMs = reader.ReadInt64(),
                X = reader.ReadSingle(),
                Y = reader.ReadSingle(),
                FadeInMs = reader.ReadInt64(),
                FadeOutMs = reader.ReadInt64(),
            });
        }
        return mines;
    }

    private static byte[] WriteBlocks(List<BlockDocument> blocks)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)blocks.Count);
        foreach (BlockDocument block in blocks)
        {
            WriteGroups(writer, block.Groups);
            writer.Write(block.Shader);
            writer.Write(block.X);
            writer.Write(block.Y);
            writer.Write(block.Width);
            writer.Write(block.Height);
        }
        writer.Flush();
        return stream.ToArray();
    }

    private static List<BlockDocument> ReadBlocks(Chunk chunk)
    {
        using BinaryReader reader = chunk.OpenReader();
        uint count = ReadCount(reader, MaxItems, "block");
        var blocks = new List<BlockDocument>((int)count);
        for (uint i = 0; i < count; i++)
        {
            blocks.Add(new BlockDocument
            {
                Groups = ReadGroups(reader),
                Shader = reader.ReadInt32(),
                X = reader.ReadSingle(),
                Y = reader.ReadSingle(),
                Width = reader.ReadSingle(),
                Height = reader.ReadSingle(),
            });
        }
        return blocks;
    }

    private static byte[] WriteSpikes(List<SpikeDocument> spikes)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)spikes.Count);
        foreach (SpikeDocument spike in spikes)
        {
            WriteGroups(writer, spike.Groups);
            writer.Write(spike.Shader);
            writer.Write(spike.X);
            writer.Write(spike.Y);
            writer.Write(spike.Width);
            writer.Write(spike.Height);
        }
        writer.Flush();
        return stream.ToArray();
    }

    private static List<SpikeDocument> ReadSpikes(Chunk chunk)
    {
        using BinaryReader reader = chunk.OpenReader();
        uint count = ReadCount(reader, MaxItems, "spike");
        var spikes = new List<SpikeDocument>((int)count);
        for (uint i = 0; i < count; i++)
        {
            spikes.Add(new SpikeDocument
            {
                Groups = ReadGroups(reader),
                Shader = reader.ReadInt32(),
                X = reader.ReadSingle(),
                Y = reader.ReadSingle(),
                Width = reader.ReadSingle(),
                Height = reader.ReadSingle(),
            });
        }
        return spikes;
    }

    private static byte[] WriteStartPoints(
        List<StartPointDocument> starts,
        StringTable strings)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)starts.Count);
        foreach (StartPointDocument start in starts)
        {
            WriteGroups(writer, start.Groups);
            strings.WriteIndex(writer, start.Name);
            writer.Write(start.TimeMs);
            writer.Write(start.X);
            writer.Write(start.Y);
            writer.Write(start.MomentumX);
            writer.Write(start.MomentumY);
            writer.Write(start.DirectionX);
            writer.Write(start.DirectionY);
        }
        writer.Flush();
        return stream.ToArray();
    }

    private static List<StartPointDocument> ReadStartPoints(
        Chunk chunk,
        StringTable strings)
    {
        using BinaryReader reader = chunk.OpenReader();
        uint count = ReadCount(reader, MaxItems, "start point");
        var starts = new List<StartPointDocument>((int)count);
        for (uint i = 0; i < count; i++)
        {
            starts.Add(new StartPointDocument
            {
                Groups = ReadGroups(reader),
                Name = strings.ReadIndex(reader),
                TimeMs = reader.ReadInt64(),
                X = reader.ReadSingle(),
                Y = reader.ReadSingle(),
                MomentumX = reader.ReadSingle(),
                MomentumY = reader.ReadSingle(),
                DirectionX = reader.ReadSingle(),
                DirectionY = reader.ReadSingle(),
            });
        }
        return starts;
    }

    private static byte[] WriteGroups(
        List<GroupDocument> groups,
        StringTable strings)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)groups.Count);
        foreach (GroupDocument group in groups)
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
        writer.Flush();
        return stream.ToArray();
    }

    private static List<GroupDocument> ReadGroups(Chunk chunk, StringTable strings)
    {
        using BinaryReader reader = chunk.OpenReader();
        uint count = ReadCount(reader, GroupLimits.Count, "group");
        var groups = new List<GroupDocument>((int)count);
        for (uint i = 0; i < count; i++)
        {
            int id = reader.ReadInt32();
            if (id is < 0 or >= GroupLimits.Count)
                throw new FormatException($"XMF group slot {id} is out of range.");

            var group = new GroupDocument
            {
                Id = id,
                Name = strings.ReadIndex(reader),
            };

            uint effectCount = ReadCount(reader, MaxEffects, "group effect");
            for (uint e = 0; e < effectCount; e++)
            {
                group.Effects.Add(new GroupEffectDocument
                {
                    Kind = ReadEnum<GroupEffectKind>(reader),
                    Start = reader.ReadSingle(),
                    End = reader.ReadSingle(),
                    Ease = ReadEnum<GroupEase>(reader),
                    Muted = reader.ReadBoolean(),
                    X = reader.ReadSingle(),
                    Y = reader.ReadSingle(),
                    Tint = strings.ReadIndex(reader),
                });
            }
            groups.Add(group);
        }
        return groups;
    }

    private static byte[] WriteShaders(List<ShaderDocument> shaders, StringTable strings)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)shaders.Count);
        foreach (ShaderDocument shader in shaders)
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
        writer.Flush();
        return stream.ToArray();
    }

    private static List<ShaderDocument> ReadShaders(Chunk chunk, StringTable strings)
    {
        using BinaryReader reader = chunk.OpenReader();
        uint count = ReadCount(reader, ShaderLimits.Count, "shader");
        var shaders = new List<ShaderDocument>((int)count);
        for (uint i = 0; i < count; i++)
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

            uint paramCount = ReadCount(reader, MaxParams, "shader parameter");
            for (uint p = 0; p < paramCount; p++)
            {
                string name = strings.ReadIndex(reader);
                uint channels = ReadCount(reader, MaxChannels, "shader channel");
                var values = new float[(int)channels];
                for (uint c = 0; c < channels; c++)
                    values[c] = reader.ReadSingle();
                shader.Params[name] = values;
            }
            shaders.Add(shader);
        }
        return shaders;
    }
}

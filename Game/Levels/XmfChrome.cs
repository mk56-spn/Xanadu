using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xanadu.Ecs;
using Xanadu.Layering;
using Xanadu.Rendering;
using Xanadu.Storage;
using Xanadu.Timeline;

namespace Xanadu.Levels;

/// <summary>
/// Everything in an XMF file that is not a placed item: the level's own
/// settings, its shaders, its groups and its layer table. Each is a record
/// (<see cref="XmfRecords"/>), so it grows by appending fields.
/// </summary>
internal static class XmfChrome
{
    private const uint MaxLayers = DrawOrder.MaxLayer * 2;
    private const int MaxName = 256;

    internal static byte[] WriteMeta(LevelDocument document, StringTable strings)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        XmfRecords.Write(writer, document, (w, d) =>
        {
            w.Write(d.Version);
            strings.WriteIndex(w, d.Id);
            strings.WriteIndex(w, d.Slug);
            strings.WriteIndex(w, d.SongId);
            strings.WriteIndex(w, d.Name);
            w.Write(d.SnapDivision);
            w.Write(d.PlayfieldWidth);
            w.Write(d.PlayfieldHeight);
            w.Write(d.PlayerShader);
            w.Write(d.FieldShader);
            strings.WriteIndex(w, d.BackgroundColor);
            strings.WriteIndex(w, d.OutsideColor);
            strings.WriteIndex(w, d.FrameColor);
        });
        writer.Flush();
        return stream.ToArray();
    }

    internal static LevelDocument ReadMeta(Chunk chunk, StringTable strings)
    {
        using BinaryReader reader = chunk.OpenReader();
        return XmfRecords.Read(reader, "meta", r => new LevelDocument
        {
            Version = r.ReadInt32(),
            Id = strings.ReadIndex(r),
            Slug = strings.ReadIndex(r),
            SongId = strings.ReadIndex(r),
            Name = strings.ReadIndex(r),
            SnapDivision = r.ReadInt32(),
            PlayfieldWidth = r.ReadInt32(),
            PlayfieldHeight = r.ReadInt32(),
            PlayerShader = r.ReadInt32(),
            FieldShader = r.ReadInt32(),
            BackgroundColor = strings.ReadIndex(r),
            OutsideColor = strings.ReadIndex(r),
            FrameColor = strings.ReadIndex(r),
        });
    }

    internal static void WriteShader(BinaryWriter writer, ShaderDocument shader, StringTable strings)
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

    internal static ShaderDocument ReadShader(BinaryReader reader, StringTable strings)
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

    /// <summary>A group: its slot and name, its effects, its item-shader track and its buffer.</summary>
    internal static void WriteGroup(BinaryWriter writer, GroupDocument group, StringTable strings)
    {
        writer.Write(group.Id);
        strings.WriteIndex(writer, group.Name);
        XmfRecords.WriteList(writer, group.Effects, (w, effect) => WriteEffect(w, effect, strings));
        writer.Write(group.Shader);
        XmfShaderKeys.Write(writer, group.ShaderKeys ?? new List<ShaderKeyDocument>());
        writer.Write(group.Buffered);
        writer.Write((byte)group.BufferBlend);
        writer.Write(new Color(group.BufferTint ?? "#ffffffff").ToRgba32());
        writer.Write(group.BufferAlpha);
        writer.Write(group.BufferShader);
        XmfShaderKeys.Write(writer, group.BufferKeys ?? new List<ShaderKeyDocument>());
    }

    internal static GroupDocument ReadGroup(BinaryReader reader, StringTable strings)
    {
        int id = reader.ReadInt32();
        if (id is < 0 or >= GroupLimits.Count)
            throw new FormatException($"XMF group slot {id} is out of range.");

        var group = new GroupDocument
        {
            Id = id,
            Name = strings.ReadIndex(reader),
            Effects = XmfRecords.ReadList(reader, XmfItemIO.MaxEffects, "group effect", r => ReadEffect(r, strings)),
            Shader = reader.ReadInt32(),
        };
        XmfShaderKeys.Read(reader, group.ShaderKeys);
        group.Buffered = reader.ReadBoolean();
        group.BufferBlend = XmfBinary.ReadEnum<BlendMode>(reader);
        group.BufferTint = "#" + new Color(reader.ReadUInt32()).ToHtml(true);
        group.BufferAlpha = reader.ReadSingle();
        group.BufferShader = reader.ReadInt32();
        XmfShaderKeys.Read(reader, group.BufferKeys);
        return group;
    }

    private static void WriteEffect(BinaryWriter writer, GroupEffectDocument effect, StringTable strings)
    {
        writer.Write((byte)effect.Kind);
        writer.Write(effect.Start);
        writer.Write(effect.End);
        writer.Write((byte)effect.Ease);
        writer.Write(effect.Muted);
        writer.Write(effect.X);
        writer.Write(effect.Y);
        strings.WriteIndex(writer, effect.Tint);
        writer.Write(effect.Cycles);
        writer.Write(effect.EveryBeats);
        writer.Write((byte)effect.Cycle);
    }

    private static GroupEffectDocument ReadEffect(BinaryReader reader, StringTable strings)
    {
        var effect = new GroupEffectDocument
        {
            Kind = XmfBinary.ReadEnum<GroupEffectKind>(reader),
            Start = reader.ReadSingle(),
            End = reader.ReadSingle(),
            Ease = XmfBinary.ReadEnum<EasePreset>(reader),
            Muted = reader.ReadBoolean(),
            X = reader.ReadSingle(),
            Y = reader.ReadSingle(),
            Tint = strings.ReadIndex(reader),
        };
        int cycles = reader.ReadInt32();
        float every = reader.ReadSingle();
        effect.Cycle = XmfBinary.ReadEnum<GroupCycle>(reader);
        effect.Cycles = Math.Clamp(cycles, 1, GroupEffect.MaxCycles);
        effect.EveryBeats = every > 0f ? every : 1f;
        return effect;
    }

    internal static void WriteLayer(BinaryWriter writer, LayerDocument layer)
    {
        writer.Write(layer.Number);
        writer.Write(Clip(layer.Name));
        writer.Write((byte)layer.Pass);
        writer.Write(layer.PassShader);
        XmfShaderKeys.Write(writer, layer.PassKeys ?? new List<ShaderKeyDocument>());
    }

    internal static LayerDocument ReadLayer(BinaryReader reader)
    {
        var layer = new LayerDocument
        {
            Number = reader.ReadInt32(),
            Name = Clip(reader.ReadString()),
            Pass = XmfBinary.ReadEnum<LayerPassKind>(reader),
            PassShader = reader.ReadInt32(),
        };
        XmfShaderKeys.Read(reader, layer.PassKeys);
        return layer;
    }

    internal static uint LayerLimit => MaxLayers;

    /// <summary>The layer table in layer order.</summary>
    internal static List<LayerDocument> Ordered(List<LayerDocument>? layers) =>
        (layers ?? new List<LayerDocument>()).OrderBy(layer => layer.Number).ToList();

    private static string Clip(string? text) =>
        text is null ? "" : text.Length > MaxName ? text[..MaxName] : text;
}

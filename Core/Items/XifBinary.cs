using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using Xanadu.Storage;

namespace Xanadu.Items;

/// <summary>
/// The binary form of a <see cref="Xif"/> inside a chunked file, shared by
/// XMF notes and XAF effects. The item itself is written first; primitive
/// geometry follows separately, so a format that predates it can keep its
/// layout and carry the geometry as an optional extension.
/// </summary>
public static class XifBinary
{
    private const uint MaxPrimitives = 1_000_000;
    private const uint MaxParams = 4_096;
    private const uint MaxChannels = 16;
    private const uint MaxPoints = 4_096;

    /// <summary>Every string an item's entry carries, so it ends up in the shared table.</summary>
    public static void Index(Xif item, StringTable strings)
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

    public static void Write(BinaryWriter writer, Xif item, StringTable strings)
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

    public static Xif Read(BinaryReader reader, StringTable strings)
    {
        var item = new Xif
        {
            Id = strings.ReadIndex(reader),
            Name = strings.ReadIndex(reader),
            Owner = ReadEnum<XifOwner>(reader),
        };

        uint primitiveCount = ReadCount(reader, MaxPrimitives, "xif primitive");
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

    public static bool HasGeometry(Xif item) => item.Primitives.Exists(primitive => primitive.HasGeometry);

    /// <summary>Each primitive's points and fill, in primitive order.</summary>
    public static void WriteGeometry(BinaryWriter writer, Xif item)
    {
        foreach (XifPrimitive primitive in item.Primitives)
        {
            writer.Write((uint)primitive.Points.Count);
            foreach (Vector2 point in primitive.Points)
            {
                writer.Write(point.X);
                writer.Write(point.Y);
            }
            writer.Write(primitive.Fill.R);
            writer.Write(primitive.Fill.G);
            writer.Write(primitive.Fill.B);
            writer.Write(primitive.Fill.A);
        }
    }

    public static void ReadGeometry(BinaryReader reader, Xif item)
    {
        foreach (XifPrimitive primitive in item.Primitives)
        {
            uint count = ReadCount(reader, MaxPoints, "xif point");
            primitive.Points = new List<Vector2>((int)count);
            for (uint i = 0; i < count; i++)
                primitive.Points.Add(new Vector2(reader.ReadSingle(), reader.ReadSingle()));
            primitive.Fill = new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }
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

    private static uint ReadCount(BinaryReader reader, uint max, string what)
    {
        uint count = reader.ReadUInt32();
        if (count > max)
            throw new FormatException($"Too many {what} entries ({count}).");
        return count;
    }

    private static T ReadEnum<T>(BinaryReader reader) where T : struct, Enum
    {
        byte value = reader.ReadByte();
        T result = (T)Enum.ToObject(typeof(T), value);
        if (!Enum.IsDefined(result))
            throw new FormatException($"Unknown {typeof(T).Name} value {value}.");
        return result;
    }
}

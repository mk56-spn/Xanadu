using System;
using System.Collections.Generic;
using System.IO;
using Xanadu.Ecs;

namespace Xanadu.Levels;

/// <summary>
/// The encoding of a shader track's keys, shared by the layer table's pass
/// track and a group's item-shader track: a count, then each key's parameter,
/// time, easing, size and up to four values.
/// </summary>
internal static class XmfShaderKeys
{
    internal const uint MaxKeys = 65_536;

    private const int MaxParam = 256;

    internal static void Write(BinaryWriter writer, IReadOnlyList<ShaderKeyDocument> keys)
    {
        writer.Write((uint)keys.Count);
        foreach (ShaderKeyDocument key in keys)
        {
            writer.Write(Clip(key.Param));
            writer.Write(key.Time);
            writer.Write((byte)key.Ease);
            float[] value = key.Value ?? [];
            int size = Math.Min(value.Length, ShaderKey.MaxSize);
            writer.Write((byte)size);
            for (int i = 0; i < size; i++)
                writer.Write(value[i]);
        }
    }

    internal static void Read(BinaryReader reader, List<ShaderKeyDocument> into)
    {
        uint count = XmfBinary.ReadCount(reader, MaxKeys, "shader key");
        for (uint i = 0; i < count; i++)
        {
            var key = new ShaderKeyDocument
            {
                Param = reader.ReadString(),
                Time = reader.ReadSingle(),
                Ease = (GroupEase)reader.ReadByte(),
            };
            int size = reader.ReadByte();
            if (size > ShaderKey.MaxSize)
                throw new FormatException("XMF shader key holds more than a vec4.");
            key.Value = new float[size];
            for (int v = 0; v < size; v++)
                key.Value[v] = reader.ReadSingle();
            into.Add(key);
        }
    }

    private static string Clip(string? text) =>
        text is null ? "" : text.Length > MaxParam ? text[..MaxParam] : text;
}

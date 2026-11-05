using System.Collections.Generic;
using System.IO;
using System.Text;
using Xanadu.Storage;

namespace Xanadu.Levels;

/// <summary>
/// Grass patches, in an optional chunk of their own: maps without grass leave
/// it out, and readers that predate it skip it.
/// </summary>
public static partial class XmfBinary
{
    internal static bool HasGrass(IChartItems items) => items.Grass is { Count: > 0 };

    internal static byte[] WriteGrass(List<GrassDocument> grass)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        WriteGrass(writer, grass);
        writer.Flush();
        return stream.ToArray();
    }

    internal static void WriteGrass(BinaryWriter writer, List<GrassDocument> grass)
    {
        writer.Write((uint)grass.Count);
        foreach (GrassDocument patch in grass)
        {
            WriteGroups(writer, patch.Groups);
            writer.Write(patch.Shader);
            writer.Write(patch.X);
            writer.Write(patch.Y);
            writer.Write(patch.Width);
            writer.Write(patch.Length);
            writer.Write(patch.Density);
            writer.Write(patch.Sway);
            writer.Write(patch.Color);
            writer.Write(patch.Variation);
            writer.Write(patch.Seed);
            writer.Write(patch.SelfModulate);
        }
    }

    internal static List<GrassDocument> ReadGrass(Chunk chunk)
    {
        using BinaryReader reader = chunk.OpenReader();
        return ReadGrass(reader);
    }

    internal static List<GrassDocument> ReadGrass(BinaryReader reader)
    {
        uint count = ReadCount(reader, MaxItems, "grass");
        var grass = new List<GrassDocument>((int)count);
        for (uint i = 0; i < count; i++)
        {
            grass.Add(new GrassDocument
            {
                Groups = ReadGroups(reader),
                Shader = reader.ReadInt32(),
                X = reader.ReadSingle(),
                Y = reader.ReadSingle(),
                Width = reader.ReadSingle(),
                Length = reader.ReadSingle(),
                Density = reader.ReadSingle(),
                Sway = reader.ReadSingle(),
                Color = reader.ReadUInt32(),
                Variation = reader.ReadSingle(),
                Seed = reader.ReadUInt32(),
                SelfModulate = reader.ReadUInt32(),
            });
        }
        return grass;
    }

    private static byte[] WriteVariantGrass(List<XmfVariant> variants)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)variants.Count);
        foreach (XmfVariant variant in variants)
            WriteGrass(writer, variant.Grass);
        writer.Flush();
        return stream.ToArray();
    }

    private static void ReadVariantGrass(Chunk chunk, List<XmfVariant> variants)
    {
        using BinaryReader reader = chunk.OpenReader();
        uint count = ReadCount(reader, MaxItems, "variant grass");
        if (count != variants.Count)
            throw new System.FormatException("XMF variant grass does not match the variant count.");
        foreach (XmfVariant variant in variants)
            variant.Grass = ReadGrass(reader);
    }
}

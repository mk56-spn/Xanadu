using System.Collections.Generic;
using System.IO;
using System.Text;
using Xanadu.Items;
using Xanadu.Storage;

namespace Xanadu.Levels;

/// <summary>
/// Filled shapes, in an optional chunk of their own: maps without any leave it
/// out, and readers that predate it skip it. Each shape's item is a Xif,
/// written the way notes write theirs, with its outlines after it.
/// </summary>
public static partial class XmfBinary
{
    internal static bool HasPolygons(IChartItems items) => items.Polygons is { Count: > 0 };

    internal static byte[] WritePolygons(List<PolygonDocument> polygons, StringTable strings)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        WritePolygons(writer, polygons, strings);
        writer.Flush();
        return stream.ToArray();
    }

    internal static void WritePolygons(BinaryWriter writer, List<PolygonDocument> polygons, StringTable strings)
    {
        writer.Write((uint)polygons.Count);
        foreach (PolygonDocument polygon in polygons)
        {
            WriteGroups(writer, polygon.Groups);
            writer.Write(polygon.Shader);
            writer.Write(polygon.X);
            writer.Write(polygon.Y);
            writer.Write(polygon.Rotation);
            writer.Write(polygon.SelfModulate);
            XmfItemIO.WriteItem(writer, polygon.Item, strings);
            XifBinary.WriteGeometry(writer, polygon.Item);
        }
    }

    internal static List<PolygonDocument> ReadPolygons(Chunk chunk, StringTable strings)
    {
        using BinaryReader reader = chunk.OpenReader();
        return ReadPolygons(reader, strings);
    }

    internal static List<PolygonDocument> ReadPolygons(BinaryReader reader, StringTable strings)
    {
        uint count = ReadCount(reader, XmfItemIO.MaxItems, "polygon");
        var polygons = new List<PolygonDocument>((int)count);
        for (uint i = 0; i < count; i++)
        {
            var polygon = new PolygonDocument
            {
                Groups = ReadGroups(reader),
                Shader = reader.ReadInt32(),
                X = reader.ReadSingle(),
                Y = reader.ReadSingle(),
                Rotation = reader.ReadSingle(),
                SelfModulate = reader.ReadUInt32(),
                Item = XmfItemIO.ReadItem(reader, strings),
            };
            XifBinary.ReadGeometry(reader, polygon.Item);
            polygons.Add(polygon);
        }
        return polygons;
    }

    private static byte[] WriteVariantPolygons(List<XmfVariant> variants, StringTable strings)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)variants.Count);
        foreach (XmfVariant variant in variants)
            WritePolygons(writer, variant.Polygons, strings);
        writer.Flush();
        return stream.ToArray();
    }

    private static void ReadVariantPolygons(Chunk chunk, List<XmfVariant> variants, StringTable strings)
    {
        using BinaryReader reader = chunk.OpenReader();
        uint count = ReadCount(reader, XmfItemIO.MaxItems, "variant polygon");
        if (count != variants.Count)
            throw new System.FormatException("XMF variant polygons do not match the variant count.");
        foreach (XmfVariant variant in variants)
            variant.Polygons = ReadPolygons(reader, strings);
    }
}

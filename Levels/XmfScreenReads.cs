using System.Collections.Generic;
using System.IO;
using System.Text;
using Xanadu.Storage;

namespace Xanadu.Levels;

/// <summary>
/// Screen-read regions in an optional chunk of their own
/// (docs/render-architecture.md step 11): maps without regions leave it out,
/// and readers that predate it skip it. Each region carries its own layer and
/// z, so the shared item-layer chunk keeps its older layout.
/// </summary>
public static partial class XmfBinary
{
    internal static bool HasScreenReads(IChartItems items) =>
        items.ScreenReads is { Count: > 0 };

    internal static byte[] WriteScreenReads(List<ScreenReadDocument> regions)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        WriteScreenReads(writer, regions);
        writer.Flush();
        return stream.ToArray();
    }

    internal static void WriteScreenReads(BinaryWriter writer, List<ScreenReadDocument> regions)
    {
        writer.Write((uint)regions.Count);
        foreach (ScreenReadDocument region in regions)
        {
            writer.Write(region.X);
            writer.Write(region.Y);
            writer.Write(region.Width);
            writer.Write(region.Height);
            // Layer and z live here rather than in the shared item-layer chunk,
            // which keeps files from before regions readable as they are.
            writer.Write(region.Layer);
            writer.Write(region.Z);
        }
    }

    internal static List<ScreenReadDocument> ReadScreenReads(Chunk chunk)
    {
        using BinaryReader reader = chunk.OpenReader();
        return ReadScreenReads(reader);
    }

    internal static List<ScreenReadDocument> ReadScreenReads(BinaryReader reader)
    {
        uint count = ReadCount(reader, XmfItemIO.MaxItems, "screen-read region");
        var regions = new List<ScreenReadDocument>((int)count);
        for (uint i = 0; i < count; i++)
        {
            regions.Add(new ScreenReadDocument
            {
                X = reader.ReadSingle(),
                Y = reader.ReadSingle(),
                Width = reader.ReadSingle(),
                Height = reader.ReadSingle(),
                Layer = reader.ReadInt32(),
                Z = reader.ReadInt32(),
            });
        }

        return regions;
    }
}

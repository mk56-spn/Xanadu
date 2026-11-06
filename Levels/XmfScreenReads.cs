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

    /// <summary>
    /// Set on the count when each region also stores its group mask. Files
    /// from before regions could join groups don't have it and still read.
    /// </summary>
    private const uint ScreenReadsWithGroups = 0x8000_0000u;

    internal static void WriteScreenReads(BinaryWriter writer, List<ScreenReadDocument> regions)
    {
        writer.Write((uint)regions.Count | ScreenReadsWithGroups);
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
            writer.Write(Xanadu.Ecs.ItemGroups.FromList(region.Groups));
        }
    }

    internal static List<ScreenReadDocument> ReadScreenReads(Chunk chunk)
    {
        using BinaryReader reader = chunk.OpenReader();
        return ReadScreenReads(reader);
    }

    /// <summary>Reads the region list from a reader at its start; public so older layouts can be checked.</summary>
    public static List<ScreenReadDocument> ReadScreenReads(BinaryReader reader)
    {
        uint raw = reader.ReadUInt32();
        bool withGroups = (raw & ScreenReadsWithGroups) != 0;
        uint count = raw & ~ScreenReadsWithGroups;
        if (count > XmfItemIO.MaxItems)
            throw new InvalidDataException($"Too many screen-read regions: {count}.");
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
                Groups = Xanadu.Ecs.ItemGroups.ToList(withGroups ? reader.ReadUInt32() : 0u),
            });
        }

        return regions;
    }
}

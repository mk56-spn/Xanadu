using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Xanadu.Levels;

/// <summary>
/// The per-item layer chunk of an XMF file: each item's layer and z, kind by
/// kind in the order the kind's own chunk lists its items. A file without it
/// reads with every item on its kind's default layer (layer 0 in the document).
/// </summary>
internal static class XmfItemLayers
{
    /// <summary>Every item kind, in the order the chunk stores them.</summary>
    private static IEnumerable<IReadOnlyList<ILayeredDocument>> Kinds(IChartItems items) =>
    [
        items.Notes, items.Mines, items.StartPoints, items.Blocks,
        items.Spikes, items.Texts, items.Grass, items.Polygons,
    ];

    internal static bool HasLayers(IChartItems items) =>
        Kinds(items).Any(kind => kind.Any(item => item.Layer != 0 || item.Z != 0));

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
        foreach (IReadOnlyList<ILayeredDocument> kind in Kinds(items))
        {
            writer.Write((uint)kind.Count);
            foreach (ILayeredDocument item in kind)
            {
                writer.Write(item.Layer);
                writer.Write(item.Z);
            }
        }
    }

    internal static void Read(BinaryReader reader, IChartItems items)
    {
        foreach (IReadOnlyList<ILayeredDocument> kind in Kinds(items))
        {
            uint count = XmfBinary.ReadCount(reader, XmfItemIO.MaxItems, "item layer");
            if (count != kind.Count)
                throw new FormatException("XMF item layers do not match their items.");
            foreach (ILayeredDocument item in kind)
            {
                item.Layer = reader.ReadInt32();
                item.Z = reader.ReadInt32();
            }
        }
    }
}

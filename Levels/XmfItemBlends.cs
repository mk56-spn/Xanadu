using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xanadu.Ecs;
using Xanadu.Rendering;

namespace Xanadu.Levels;

/// <summary>
/// The per-item blend mode chunk of an XMF file: each item's mode, kind by
/// kind in the order the kind's own chunk lists its items. A file without it
/// reads with every item on <see cref="BlendMode.Mix"/>.
/// </summary>
internal static class XmfItemBlends
{
    /// <summary>Every item kind, in the order the chunk stores them.</summary>
    private static IEnumerable<IReadOnlyList<IBlendedDocument>> Kinds(IChartItems items) =>
    [
        items.Notes, items.Mines, items.StartPoints, items.Blocks,
        items.Spikes, items.Texts, items.Grass, items.Polygons,
    ];

    internal static bool HasBlends(IChartItems items) =>
        Kinds(items).Any(kind => kind.Any(item => item.Blend != BlendMode.Mix));

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
        foreach (IReadOnlyList<IBlendedDocument> kind in Kinds(items))
        {
            writer.Write((uint)kind.Count);
            foreach (IBlendedDocument item in kind)
                writer.Write((byte)item.Blend);
        }
    }

    internal static void Read(BinaryReader reader, IChartItems items)
    {
        foreach (IReadOnlyList<IBlendedDocument> kind in Kinds(items))
        {
            uint count = XmfBinary.ReadCount(reader, XmfItemIO.MaxItems, "item blend");
            if (count != kind.Count)
                throw new FormatException("XMF item blends do not match their items.");
            foreach (IBlendedDocument item in kind)
            {
                int mode = reader.ReadByte();
                if (!Enum.IsDefined(typeof(BlendMode), mode))
                    throw new FormatException($"XMF item blend {mode} is not a blend mode.");
                item.Blend = (BlendMode)mode;
            }
        }
    }
}

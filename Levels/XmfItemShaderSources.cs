using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Xanadu.Levels;

/// <summary>
/// The per-item shader-source chunk of an XMF file: each item's chosen group
/// (0–15), or -1 for its own shader, kind by kind in the order the kind's own
/// chunk lists its items. Left out when every item uses its own shader.
/// </summary>
internal static class XmfItemShaderSources
{
    /// <summary>Every item kind, in the order the chunk stores them.</summary>
    private static IEnumerable<IReadOnlyList<IShaderSourcedDocument>> Kinds(IChartItems items) =>
    [
        items.Notes, items.Mines, items.StartPoints, items.Blocks,
        items.Spikes, items.Texts, items.Grass, items.Polygons,
    ];

    internal static bool HasSources(IChartItems items) =>
        Kinds(items).Any(kind => kind.Any(item => item.ShaderSource != -1));

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
        foreach (IReadOnlyList<IShaderSourcedDocument> kind in Kinds(items))
        {
            writer.Write((uint)kind.Count);
            foreach (IShaderSourcedDocument item in kind)
                writer.Write((sbyte)item.ShaderSource);
        }
    }

    internal static void Read(BinaryReader reader, IChartItems items)
    {
        foreach (IReadOnlyList<IShaderSourcedDocument> kind in Kinds(items))
        {
            uint count = XmfBinary.ReadCount(reader, XmfItemIO.MaxItems, "item shader source");
            if (count != kind.Count)
                throw new FormatException("XMF item shader sources do not match their items.");
            foreach (IShaderSourcedDocument item in kind)
            {
                int source = reader.ReadSByte();
                if (source < -1 || source >= Xanadu.Ecs.GroupLimits.Count)
                    throw new FormatException($"XMF item shader source {source} is not a group.");
                item.ShaderSource = source;
            }
        }
    }
}

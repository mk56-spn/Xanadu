using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xanadu.Ecs;
using Xanadu.Items;
using Xanadu.Settings;
using Xanadu.Storage;

namespace Xanadu.Levels;

/// <summary>The per-item colour chunk of an XMF file.</summary>
internal static class XmfItemColors
{
    internal static bool HasSelfModulates(IChartItems items) =>
        items.Notes.Exists(item => item.SelfModulate != SelfModulate.White) ||
        items.Mines.Exists(item => item.SelfModulate != SelfModulate.White) ||
        items.Blocks.Exists(item => item.SelfModulate != SelfModulate.White) ||
        items.Spikes.Exists(item => item.SelfModulate != SelfModulate.White);

    /// <summary>Each item kind's colours in the order its own chunk lists the items.</summary>
    internal static byte[] WriteSelfModulates(IChartItems items)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        WriteSelfModulates(writer, items);
        writer.Flush();
        return stream.ToArray();
    }

    internal static void WriteSelfModulates(BinaryWriter writer, IChartItems items)
    {
        WriteColors(writer, items.Notes.ConvertAll(item => item.SelfModulate));
        WriteColors(writer, items.Mines.ConvertAll(item => item.SelfModulate));
        WriteColors(writer, items.Blocks.ConvertAll(item => item.SelfModulate));
        WriteColors(writer, items.Spikes.ConvertAll(item => item.SelfModulate));
    }

    internal static void ReadSelfModulates(BinaryReader reader, IChartItems items)
    {
        ReadColors(reader, items.Notes.Count, (i, color) => items.Notes[i].SelfModulate = color);
        ReadColors(reader, items.Mines.Count, (i, color) => items.Mines[i].SelfModulate = color);
        ReadColors(reader, items.Blocks.Count, (i, color) => items.Blocks[i].SelfModulate = color);
        ReadColors(reader, items.Spikes.Count, (i, color) => items.Spikes[i].SelfModulate = color);
    }

    internal static void WriteColors(BinaryWriter writer, List<uint> colors)
    {
        writer.Write((uint)colors.Count);
        foreach (uint color in colors)
            writer.Write(color);
    }

    internal static void ReadColors(BinaryReader reader, int expected, Action<int, uint> assign)
    {
        uint count = XmfBinary.ReadCount(reader, XmfItemIO.MaxItems, "self modulate");
        if (count != expected)
            throw new FormatException("XMF self modulates do not match their items.");
        for (int i = 0; i < expected; i++)
            assign(i, reader.ReadUInt32());
    }
}

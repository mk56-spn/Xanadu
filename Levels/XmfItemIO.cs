using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xanadu.Ecs;
using Xanadu.Items;
using Xanadu.Settings;
using Xanadu.Storage;

namespace Xanadu.Levels;

/// <summary>The item primitives every XMF chunk shares: counts, items and their string indexes.</summary>
internal static class XmfItemIO
{
    internal const uint MaxItems = 1_000_000;
    internal const uint MaxEffects = 100_000;
    internal const uint MaxParams = 4_096;
    internal const uint MaxChannels = 16;



    /// <summary>Writes a counted list whose items all have the same fields; the item writer does the fields.</summary>
    internal static byte[] WriteList<T>(List<T> items, Action<BinaryWriter, T> write)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)items.Count);
        foreach (T item in items)
            write(writer, item);
        writer.Flush();
        return stream.ToArray();
    }

    /// <summary>Reads a counted list whose items all have the same fields; the item reader does the fields.</summary>
    internal static List<T> ReadList<T>(Chunk chunk, uint max, string what, Func<BinaryReader, T> read)
    {
        using BinaryReader reader = chunk.OpenReader();
        uint count = XmfBinary.ReadCount(reader, max, what);
        var items = new List<T>((int)count);
        for (uint i = 0; i < count; i++)
            items.Add(read(reader));
        return items;
    }

    internal static void IndexItemStrings(Xif item, StringTable strings) => XifBinary.Index(item, strings);

    internal static void WriteItem(BinaryWriter writer, Xif item, StringTable strings) =>
        XifBinary.Write(writer, item, strings);

    internal static Xif ReadItem(BinaryReader reader, StringTable strings) => XifBinary.Read(reader, strings);
}

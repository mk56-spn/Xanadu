using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xanadu.Ecs;
using Xanadu.Rendering;
using Xanadu.Storage;

namespace Xanadu.Levels;

/// <summary>
/// How every XMF list is laid out: a count, then each entry as a record, a
/// byte length followed by that many bytes. A reader reads the fields it
/// knows and skips the rest of the record, so a field added later is
/// appended to its record and read only when the record still has bytes
/// (<see cref="More"/>); a file written before it reads with the default.
/// </summary>
internal static class XmfRecords
{
    /// <summary>A whole list chunk: the count, then one record per entry.</summary>
    internal static byte[] WriteList<T>(IReadOnlyList<T> entries, Action<BinaryWriter, T> write)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        WriteList(writer, entries, write);
        writer.Flush();
        return stream.ToArray();
    }

    internal static void WriteList<T>(BinaryWriter writer, IReadOnlyList<T> entries, Action<BinaryWriter, T> write)
    {
        writer.Write((uint)entries.Count);
        foreach (T entry in entries)
            Write(writer, entry, write);
    }

    internal static List<T> ReadList<T>(Chunk chunk, uint max, string what, Func<BinaryReader, T> read)
    {
        using BinaryReader reader = chunk.OpenReader();
        return ReadList(reader, max, what, read);
    }

    internal static List<T> ReadList<T>(BinaryReader reader, uint max, string what, Func<BinaryReader, T> read)
    {
        uint count = XmfBinary.ReadCount(reader, max, what);
        var entries = new List<T>((int)Math.Min(count, 4096));
        for (uint i = 0; i < count; i++)
            entries.Add(Read(reader, what, read));
        return entries;
    }

    /// <summary>One record: its fields, framed by their byte length.</summary>
    internal static void Write<T>(BinaryWriter writer, T entry, Action<BinaryWriter, T> write)
    {
        using var stream = new MemoryStream();
        using (var body = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            write(body, entry);
        writer.Write((uint)stream.Length);
        writer.Write(stream.GetBuffer(), 0, (int)stream.Length);
    }

    /// <summary>Reads one record with its own reader, which ends where the record does.</summary>
    internal static T Read<T>(BinaryReader reader, string what, Func<BinaryReader, T> read)
    {
        uint length = reader.ReadUInt32();
        if (length > reader.BaseStream.Length - reader.BaseStream.Position)
            throw new FormatException($"XMF {what} record runs past its chunk.");

        byte[] bytes = reader.ReadBytes((int)length);
        using var record = new BinaryReader(new MemoryStream(bytes, writable: false), Encoding.UTF8);
        try
        {
            return read(record);
        }
        catch (EndOfStreamException)
        {
            throw new FormatException($"XMF {what} record is truncated.");
        }
    }

    /// <summary>Whether a record still has fields after the ones read so far.</summary>
    internal static bool More(BinaryReader record) => record.BaseStream.Position < record.BaseStream.Length;

    /// <summary>The header every item record starts with: what <see cref="ItemDocument"/> holds.</summary>
    internal static void WriteHeader(BinaryWriter writer, ItemDocument item)
    {
        writer.Write(ItemGroups.FromList(item.Groups));
        writer.Write(item.Shader);
        writer.Write((sbyte)item.ShaderSource);
        writer.Write(item.Layer);
        writer.Write(item.Z);
        writer.Write((byte)item.Blend);
        writer.Write(item.SelfModulate);
    }

    internal static void ReadHeader(BinaryReader reader, ItemDocument item)
    {
        item.Groups = ItemGroups.ToList(reader.ReadUInt32());
        item.Shader = reader.ReadInt32();
        int source = reader.ReadSByte();
        if (source < -1 || source >= GroupLimits.Count)
            throw new FormatException($"XMF item shader source {source} is not a group.");
        item.ShaderSource = source;
        item.Layer = reader.ReadInt32();
        item.Z = reader.ReadInt32();
        item.Blend = XmfBinary.ReadEnum<BlendMode>(reader);
        item.SelfModulate = reader.ReadUInt32();
    }

    /// <summary>An item record: the shared header, then the kind's own fields.</summary>
    internal static byte[] WriteItems<T>(IReadOnlyList<T> items, Action<BinaryWriter, T> body)
        where T : ItemDocument =>
        WriteList(items, (writer, item) =>
        {
            WriteHeader(writer, item);
            body(writer, item);
        });

    internal static List<T> ReadItems<T>(Chunk chunk, string what, Action<BinaryReader, T> body)
        where T : ItemDocument, new() =>
        ReadList(chunk, XmfItemIO.MaxItems, what, reader =>
        {
            var item = new T();
            ReadHeader(reader, item);
            body(reader, item);
            return item;
        });
}

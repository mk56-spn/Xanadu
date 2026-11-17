using System;
using System.Collections.Generic;
using System.IO;
using Xanadu.Core.Storage;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Settings;

namespace Xanadu.Model.Levels;

/// <summary>An XMF map file that could not be read.</summary>
public sealed class XmfFormatException(string message) : Exception(message);

/// <summary>The chunks of an XMF map file. The notes chunk is left out when there are none.</summary>
public enum XmfChunk : uint
{
    Strings = 1,
    Meta = 2,
    Notes = 10,
}

/// <summary>
/// Binary XMF map format, 1.2: the level's settings and its notes. 1.2 notes
/// carry a route (turn times and one speed choice); 1.0 and 1.1 files carried
/// free-form curve paths, which are read past and their notes kept as plain notes. It shares
/// its container (magic, version, chunk directory, string table) with XAF
/// packages, and every list is a count of length-framed records
/// (<see cref="XmfRecords"/>), so a record grows by appending fields and a new
/// kind of thing is a new chunk. 0.x files, from the classic game, are not read.
/// </summary>
public static class XmfBinary
{
    public const ushort MajorVersion = 1;
    public const ushort MinorVersion = 3;

    /// <summary>The extension level files are saved with, including the dot.</summary>
    public const string Extension = ".xmf";

    /// <summary>The most notes one level may hold.</summary>
    public const uint MaxNotes = 100_000;

    private const string Format = "XMF";

    private static readonly byte[] Magic = "XMF1"u8.ToArray();

    public static void Save(string path, LevelDocument document) =>
        BinaryFile.Save(path, Write(document));

    public static byte[] Write(LevelDocument document)
    {
        var strings = new StringTable(Format);
        byte[] meta = WriteMeta(document, strings);
        byte[]? notes = document.Notes.Count > 0 ? XmfRecords.WriteList(document.Notes, WriteNote) : null;

        // The table is written first in the file but last here, once every chunk has put its strings in it.
        var file = new ChunkWriter(Magic, MajorVersion, MinorVersion);
        file.Add((uint)XmfChunk.Strings, strings.Write());
        file.Add((uint)XmfChunk.Meta, meta);
        if (notes is not null)
            file.Add((uint)XmfChunk.Notes, notes);
        return file.ToArray();
    }

    public static LevelDocument Read(byte[] data)
    {
        try
        {
            ChunkReader file = ChunkReader.Open(data, Magic, MajorVersion, Format);
            StringTable strings = StringTable.Read(file.Get((uint)XmfChunk.Strings, Format), Format);
            LevelDocument document = ReadMeta(file.Get((uint)XmfChunk.Meta, Format), strings);
            document.Notes = file.TryGet((uint)XmfChunk.Notes, out Chunk notes)
                ? XmfRecords.ReadList(notes, MaxNotes, "note", reader => ReadNote(reader, file.Minor))
                : new List<NoteDocument>();
            return document;
        }
        catch (Exception exception) when (exception is FormatException or IOException)
        {
            throw new XmfFormatException(exception.Message);
        }
    }

    private static byte[] WriteMeta(LevelDocument document, StringTable strings)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        XmfRecords.Write(writer, document, (w, d) =>
        {
            strings.WriteIndex(w, d.Id);
            strings.WriteIndex(w, d.Slug);
            strings.WriteIndex(w, d.SongId);
            strings.WriteIndex(w, d.Name);
            w.Write(d.SnapDivision);
            w.Write(d.PlayfieldWidth);
            w.Write(d.PlayfieldHeight);
        });
        writer.Flush();
        return stream.ToArray();
    }

    private static LevelDocument ReadMeta(Chunk chunk, StringTable strings)
    {
        using BinaryReader reader = chunk.OpenReader();
        return XmfRecords.Read(reader, "meta", r => new LevelDocument
        {
            Id = strings.ReadIndex(r),
            Slug = strings.ReadIndex(r),
            SongId = strings.ReadIndex(r),
            Name = strings.ReadIndex(r),
            SnapDivision = r.ReadInt32(),
            PlayfieldWidth = r.ReadInt32(),
            PlayfieldHeight = r.ReadInt32(),
        });
    }

    /// <summary>A note: time, place, key, then a route note's turn times (a count, then ms after the head each) and its speed choice, then (1.3) a dash's length and a hold's tail time and offset.</summary>
    private static void WriteNote(BinaryWriter writer, NoteDocument note)
    {
        writer.Write(note.TimeMs);
        writer.Write(note.X);
        writer.Write(note.Y);
        writer.Write((byte)note.Target);
        List<long> route = note.Route ?? [];
        writer.Write((ushort)route.Count);
        foreach (long time in route)
            writer.Write(time);
        writer.Write((byte)note.RouteStrength);
        writer.Write(note.DashLength);
        writer.Write(note.HoldTailMs);
        writer.Write(note.HoldX);
        writer.Write(note.HoldY);
    }

    private static NoteDocument ReadNote(BinaryReader reader, ushort minor)
    {
        var note = new NoteDocument
        {
            TimeMs = reader.ReadInt64(),
            X = reader.ReadSingle(),
            Y = reader.ReadSingle(),
            Target = ReadEnum<KeybindAction>(reader),
        };
        if (!float.IsFinite(note.X) || !float.IsFinite(note.Y))
            throw new FormatException("XMF note is not on the field.");

        // Before 1.2 the rest was a curve path, which routes replaced: the note stays, plain.
        if (minor < 2)
            return note;

        int count = reader.ReadUInt16();
        if (count > NoteRoute.MaxTimes)
            throw new FormatException("XMF route note has too many turns.");
        if (count > 0)
        {
            note.Route = new List<long>(count);
            for (int i = 0; i < count; i++)
                note.Route.Add(reader.ReadInt64());
        }

        note.RouteStrength = ReadEnum<DashStrength>(reader);

        // 1.3 added dash lengths and hold tails.
        if (minor < 3)
            return note;

        note.DashLength = reader.ReadSingle();
        note.HoldTailMs = reader.ReadInt64();
        note.HoldX = reader.ReadSingle();
        note.HoldY = reader.ReadSingle();
        if (!float.IsFinite(note.DashLength) || !float.IsFinite(note.HoldX) || !float.IsFinite(note.HoldY))
            throw new FormatException("XMF note has a broken dash or hold.");
        return note;
    }

    private static T ReadEnum<T>(BinaryReader reader) where T : struct, Enum
    {
        byte value = reader.ReadByte();
        T result = (T)Enum.ToObject(typeof(T), value);
        if (!Enum.IsDefined(result))
            throw new FormatException($"XMF {typeof(T).Name} value {value} is out of range.");
        return result;
    }
}

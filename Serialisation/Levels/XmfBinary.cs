using Xanadu.Model.Levels;
using System;
using System.Collections.Generic;
using System.IO;
using Xanadu.Core.Storage;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Settings;

namespace Xanadu.Serialisation.Levels;

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
/// Binary XMF map format, 1.4: the level's settings and its notes, each a time,
/// a place and a key. Older files' notes carried more (curve paths before 1.2,
/// routes from 1.2, dashes and holds from 1.3); records are length-framed, so
/// those fields are read past and the notes kept as plain notes. It shares
/// its container (magic, version, chunk directory, string table) with XAF
/// packages, and every list is a count of length-framed records
/// (<see cref="XmfRecords"/>), so a record grows by appending fields and a new
/// kind of thing is a new chunk. 0.x files, from the classic game, are not read.
/// </summary>
public static class XmfBinary
{
    public const ushort MajorVersion = 1;
    public const ushort MinorVersion = 4;

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
                ? XmfRecords.ReadList(notes, MaxNotes, "note", ReadNote)
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

    /// <summary>A note: time, place, key.</summary>
    private static void WriteNote(BinaryWriter writer, NoteDocument note)
    {
        writer.Write(note.TimeMs);
        writer.Write(note.X);
        writer.Write(note.Y);
        writer.Write((byte)note.Target);
    }

    /// <summary>Reads a note's time, place and key; anything an older version wrote after them is left unread.</summary>
    private static NoteDocument ReadNote(BinaryReader reader)
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

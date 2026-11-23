using Xanadu.Model.Levels;
using System;
using System.Collections.Generic;
using System.IO;
using Xanadu.Core.Storage;
using Xanadu.Serialisation.Items;
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
    Chords = 11,
    Pads = 12,
    Drafts = 13,
    DashStops = 14,
    Decos = 15,

    /// <summary>The level's groups: a name, the ids of the items held, and a clip on the song's beats.</summary>
    Groups = 16,
}

/// <summary>
/// Binary XMF map format, 1.0: the level's settings, its notes (each a time,
/// a place, a key and a kind; an orb adds its tail and height path), its
/// chords (a time and an anchor), its decos (an origin and a Xif tree), and its
/// groups (item ids and a clip on the song's beats). It shares
/// its container (magic, version, chunk directory, string table) with XIF
/// files, and every list is a count of length-framed records
/// (<see cref="XmfRecords"/>), so a record grows by appending fields and a new
/// kind of thing is a new chunk.
/// </summary>
public static class XmfBinary
{
    public const ushort MajorVersion = 1;
    public const ushort MinorVersion = 0;

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
        byte[]? chords = document.Chords.Count > 0 ? XmfRecords.WriteList(document.Chords, WriteChord) : null;
        byte[]? pads = document.Pads.Count > 0 ? XmfRecords.WriteList(document.Pads, WritePad) : null;
        byte[]? drafts = document.Drafts.Count > 0 ? XmfRecords.WriteList(document.Drafts, WriteDraft) : null;
        byte[]? stops = document.DashStops.Count > 0 ? XmfRecords.WriteList(document.DashStops, WriteDashStop) : null;
        byte[]? decos = null;
        if (document.Decos.Count > 0)
        {
            foreach (DecoDocument deco in document.Decos)
                XifBinary.Index(deco.Xif, strings);
            decos = XmfRecords.WriteList(document.Decos, (w, d) => WriteDeco(w, d, strings));
        }

        byte[]? groups = null;
        if (document.Groups.Count > 0)
        {
            foreach (GroupDocument group in document.Groups)
            {
                strings.Add(group.Name);
                foreach (string member in group.Members)
                    strings.Add(member);
                KeyBinary.Index(group.Clip.Channels, strings);
            }
            groups = XmfRecords.WriteList(document.Groups, (w, g) => WriteGroup(w, g, strings));
        }

        // The table is written first in the file but last here, once every chunk has put its strings in it.
        var file = new ChunkWriter(Magic, MajorVersion, MinorVersion);
        file.Add((uint)XmfChunk.Strings, strings.Write());
        file.Add((uint)XmfChunk.Meta, meta);
        if (notes is not null)
            file.Add((uint)XmfChunk.Notes, notes);
        if (chords is not null)
            file.Add((uint)XmfChunk.Chords, chords);
        if (pads is not null)
            file.Add((uint)XmfChunk.Pads, pads);
        if (drafts is not null)
            file.Add((uint)XmfChunk.Drafts, drafts);
        if (stops is not null)
            file.Add((uint)XmfChunk.DashStops, stops);
        if (decos is not null)
            file.Add((uint)XmfChunk.Decos, decos);
        if (groups is not null)
            file.Add((uint)XmfChunk.Groups, groups);
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
            document.Chords = file.TryGet((uint)XmfChunk.Chords, out Chunk chords)
                ? XmfRecords.ReadList(chords, MaxNotes, "chord", ReadChord)
                : new List<ChordDocument>();
            document.Pads = file.TryGet((uint)XmfChunk.Pads, out Chunk pads)
                ? XmfRecords.ReadList(pads, MaxNotes, "pad", ReadPad)
                : new List<PadDocument>();
            document.Drafts = file.TryGet((uint)XmfChunk.Drafts, out Chunk drafts)
                ? XmfRecords.ReadList(drafts, MaxNotes, "draft", ReadDraft)
                : new List<DraftDocument>();
            document.DashStops = file.TryGet((uint)XmfChunk.DashStops, out Chunk stops)
                ? XmfRecords.ReadList(stops, MaxNotes, "dash ender", ReadDashStop)
                : new List<DashStopDocument>();
            document.Decos = file.TryGet((uint)XmfChunk.Decos, out Chunk decos)
                ? XmfRecords.ReadList(decos, MaxNotes, "deco", r => ReadDeco(r, strings))
                : new List<DecoDocument>();
            document.Groups = file.TryGet((uint)XmfChunk.Groups, out Chunk groups)
                ? XmfRecords.ReadList(groups, MaxNotes, "group", r => ReadGroup(r, strings))
                : new List<GroupDocument>();
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

    /// <summary>A note: time, place, key, kind; an entrance adds its exit; an orb, or an entrance out on one, adds its tail and height path.</summary>
    private static void WriteNote(BinaryWriter writer, NoteDocument note)
    {
        writer.Write(note.TimeMs);
        writer.Write(note.X);
        writer.Write(note.Y);
        writer.Write((byte)note.Target);
        writer.Write((byte)note.Kind);
        if (note.Kind == NoteKind.Entrance)
            writer.Write((byte)note.Exit);
        if (!note.HasOrb)
            return;

        writer.Write(note.EndMs);
        OrbKey[] path = note.Path ?? [];
        writer.Write((ushort)path.Length);
        foreach (OrbKey key in path)
        {
            writer.Write(key.OffsetMs);
            writer.Write(key.Rise);
        }
    }

    /// <summary>Reads a note: time, place, key, kind, an entrance's exit, and an orb's tail and path.</summary>
    private static NoteDocument ReadNote(BinaryReader reader)
    {
        var note = new NoteDocument
        {
            TimeMs = reader.ReadInt64(),
            X = reader.ReadSingle(),
            Y = reader.ReadSingle(),
            Target = ReadEnum<KeybindAction>(reader),
            Kind = ReadEnum<NoteKind>(reader),
        };
        if (note.Kind == NoteKind.Entrance)
            note.Exit = ReadEnum<EntranceExit>(reader);
        if (note.HasOrb)
        {
            note.EndMs = Math.Max(note.TimeMs, reader.ReadInt64());
            var path = new OrbKey[reader.ReadUInt16()];
            for (int i = 0; i < path.Length; i++)
            {
                path[i] = new OrbKey(reader.ReadInt64(), reader.ReadSingle());
                if (!float.IsFinite(path[i].Rise))
                    throw new FormatException("XMF orb path is not on the field.");
            }

            note.Path = path;
        }
        if (!float.IsFinite(note.X) || !float.IsFinite(note.Y))
            throw new FormatException("XMF note is not on the field.");
        return note;
    }

    /// <summary>A chord: time, then where its first line meets the judgement line.</summary>
    private static void WriteChord(BinaryWriter writer, ChordDocument chord)
    {
        writer.Write(chord.TimeMs);
        writer.Write(chord.X);
        writer.Write(chord.Y);
    }

    private static ChordDocument ReadChord(BinaryReader reader)
    {
        var chord = new ChordDocument
        {
            TimeMs = reader.ReadInt64(),
            X = reader.ReadSingle(),
            Y = reader.ReadSingle(),
        };
        if (!float.IsFinite(chord.X) || !float.IsFinite(chord.Y))
            throw new FormatException("XMF chord is not on the field.");
        return chord;
    }

    /// <summary>A pad: its centre, its launch speed, and its time (-1 fires on touch).</summary>
    private static void WritePad(BinaryWriter writer, PadDocument pad)
    {
        writer.Write(pad.X);
        writer.Write(pad.Y);
        writer.Write(pad.Speed);
        writer.Write(pad.TimeMs);
    }

    private static PadDocument ReadPad(BinaryReader reader)
    {
        var pad = new PadDocument { X = reader.ReadSingle(), Y = reader.ReadSingle(), Speed = reader.ReadSingle(), TimeMs = reader.ReadInt64() };
        if (!float.IsFinite(pad.X) || !float.IsFinite(pad.Y) || !float.IsFinite(pad.Speed))
            throw new FormatException("XMF pad is not on the field.");
        return pad;
    }

    /// <summary>An updraft: its emitter's centre, then its hover height.</summary>
    private static void WriteDraft(BinaryWriter writer, DraftDocument draft)
    {
        writer.Write(draft.X);
        writer.Write(draft.Y);
        writer.Write(draft.Height);
    }

    private static DraftDocument ReadDraft(BinaryReader reader)
    {
        var draft = new DraftDocument { X = reader.ReadSingle(), Y = reader.ReadSingle(), Height = reader.ReadSingle() };
        if (!float.IsFinite(draft.X) || !float.IsFinite(draft.Y) || !float.IsFinite(draft.Height))
            throw new FormatException("XMF draft is not on the field.");
        return draft;
    }

    /// <summary>A dash ender: its centre.</summary>
    private static void WriteDashStop(BinaryWriter writer, DashStopDocument stop)
    {
        writer.Write(stop.X);
        writer.Write(stop.Y);
    }

    private static DashStopDocument ReadDashStop(BinaryReader reader)
    {
        var stop = new DashStopDocument { X = reader.ReadSingle(), Y = reader.ReadSingle() };
        if (!float.IsFinite(stop.X) || !float.IsFinite(stop.Y))
            throw new FormatException("XMF dash ender is not on the field.");
        return stop;
    }

    /// <summary>A deco: its origin, then its Xif tree (XIF's flat node list, sharing the map's string table).</summary>
    private static void WriteGroup(BinaryWriter writer, GroupDocument group, StringTable strings)
    {
        strings.WriteIndex(writer, group.Name);
        writer.Write((uint)group.Members.Count);
        foreach (string member in group.Members)
            strings.WriteIndex(writer, member);
        writer.Write(group.Clip.Duration);
        writer.Write(group.Clip.Loop);
        writer.Write(group.Clip.LoopStart);
        writer.Write(group.Clip.LoopEnd);
        KeyBinary.WriteChannels(writer, group.Clip.Channels, strings);
    }

    private static GroupDocument ReadGroup(BinaryReader reader, StringTable strings)
    {
        var group = new GroupDocument { Name = strings.ReadIndex(reader) };
        uint members = reader.ReadUInt32();
        if (members > MaxNotes)
            throw new FormatException("Too many XMF group members.");
        for (uint i = 0; i < members; i++)
            group.Members.Add(strings.ReadIndex(reader));
        group.Clip.Duration = reader.ReadSingle();
        group.Clip.Loop = reader.ReadBoolean();
        group.Clip.LoopStart = reader.ReadSingle();
        group.Clip.LoopEnd = reader.ReadSingle();
        KeyBinary.ReadChannels(reader, group.Clip.Channels, strings);
        return group;
    }

    private static void WriteDeco(BinaryWriter writer, DecoDocument deco, StringTable strings)
    {
        writer.Write(deco.X);
        writer.Write(deco.Y);
        XifBinary.Write(writer, deco.Xif, strings);
    }

    private static DecoDocument ReadDeco(BinaryReader reader, StringTable strings)
    {
        var deco = new DecoDocument { X = reader.ReadSingle(), Y = reader.ReadSingle() };
        if (!float.IsFinite(deco.X) || !float.IsFinite(deco.Y))
            throw new FormatException("XMF deco is not on the field.");
        deco.Xif = XifBinary.Read(reader, strings);
        return deco;
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

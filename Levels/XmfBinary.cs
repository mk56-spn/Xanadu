using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xanadu.Ecs;
using Xanadu.Storage;

namespace Xanadu.Levels;

/// <summary>An XMF map file that could not be read.</summary>
public sealed class XmfFormatException(string message) : Exception(message);

/// <summary>
/// Binary XMF1 map format: a level split into chunks of notes, mines, blocks,
/// groups and shaders, interned in a string table. It shares its container —
/// magic, version, chunk directory and string table — with XAF packages.
/// </summary>
public static partial class XmfBinary
{
    public const ushort MajorVersion = 2;
    public const ushort MinorVersion = 0;

    /// <summary>The extension level files are saved with, including the dot.</summary>
    public const string Extension = ".xmf";

    private const string Format = "XMF";

    private static readonly byte[] Magic = "XMF1"u8.ToArray();

    private enum ChunkId : uint
    {
        Strings = 1,
        Meta = 2,
        Notes = 3,
        Mines = 4,
        Blocks = 5,
        StartPoints = 6,
        Groups = 7,
        Shaders = 8,

        Spikes = 9,

        /// <summary>Optional pair index and momentum data, absent in older maps.</summary>
        NoteExtras = 10,

        /// <summary>Optional item colours, absent when every item is untinted.</summary>
        SelfModulates = 12,

        /// <summary>Spike outlines, absent in maps from before spikes had them.</summary>
        SpikeOutlines = 13,

        /// <summary>Text written into the level, absent when it has none.</summary>
        Texts = 14,

        /// <summary>Grass patches, absent when the level has none.</summary>
        Grass = 15,
    }

    public static byte[] Write(LevelDocument document)
    {
        var strings = new StringTable(Format);
        IndexStrings(document, strings);

        var file = new ChunkWriter(Magic, MajorVersion, MinorVersion);
        file.Add((uint)ChunkId.Strings, strings.Write());
        file.Add((uint)ChunkId.Meta, WriteMeta(document, strings));
        file.Add((uint)ChunkId.Notes, WriteNotes(document.Notes, strings));
        file.Add((uint)ChunkId.Mines, WriteMines(document.Mines));
        file.Add((uint)ChunkId.Blocks, WriteBlocks(document.Blocks));
        file.Add((uint)ChunkId.StartPoints, WriteStartPoints(document.StartPoints, strings));
        file.Add((uint)ChunkId.Groups, WriteGroups(document.Groups, strings));
        file.Add((uint)ChunkId.Shaders, WriteShaders(document.Shaders, strings));
        file.Add((uint)ChunkId.Spikes, WriteSpikes(document.Spikes));
        if (HasNoteExtras(document.Notes))
            file.Add((uint)ChunkId.NoteExtras, WriteNoteExtras(document.Notes));
        if (HasSelfModulates(document))
            file.Add((uint)ChunkId.SelfModulates, WriteSelfModulates(document));
        file.Add((uint)ChunkId.SpikeOutlines, WriteSpikeOutlines(document.Spikes));
        if (HasTexts(document))
            file.Add((uint)ChunkId.Texts, WriteTexts(document.Texts));
        if (HasGrass(document))
            file.Add((uint)ChunkId.Grass, WriteGrass(document.Grass));
        return file.ToArray();
    }

    public static LevelDocument Read(byte[] data)
    {
        try
        {
            return ReadMap(data);
        }
        catch (Exception exception) when (exception is FormatException or IOException)
        {
            throw new XmfFormatException(exception.Message);
        }
    }

    private static LevelDocument ReadMap(byte[] data)
    {
        ChunkReader file = ChunkReader.Open(data, Magic, MajorVersion, Format);
        StringTable strings = StringTable.Read(
            file.Get((uint)ChunkId.Strings, Format),
            Format);

        LevelDocument document = ReadMeta(file.Get((uint)ChunkId.Meta, Format), strings);
        document.Notes = ReadNotes(file.Get((uint)ChunkId.Notes, Format), strings);
        if (file.TryGet((uint)ChunkId.NoteExtras, out Chunk noteExtras))
            ReadNoteExtras(noteExtras, document.Notes);
        document.Mines = ReadMines(file.Get((uint)ChunkId.Mines, Format));
        document.Blocks = ReadBlocks(file.Get((uint)ChunkId.Blocks, Format));
        document.StartPoints = ReadStartPoints(
            file.Get((uint)ChunkId.StartPoints, Format),
            strings);
        document.Groups = ReadGroups(file.Get((uint)ChunkId.Groups, Format), strings);
        document.Shaders = ReadShaders(file.Get((uint)ChunkId.Shaders, Format), strings);
        document.Spikes = ReadSpikes(file.Get((uint)ChunkId.Spikes, Format));
        if (file.TryGet((uint)ChunkId.SelfModulates, out Chunk selfModulates))
        {
            using BinaryReader reader = selfModulates.OpenReader();
            ReadSelfModulates(reader, document);
        }
        if (file.TryGet((uint)ChunkId.SpikeOutlines, out Chunk spikeOutlines))
        {
            using BinaryReader reader = spikeOutlines.OpenReader();
            ReadSpikeOutlines(reader, document.Spikes);
        }
        if (file.TryGet((uint)ChunkId.Texts, out Chunk texts))
            document.Texts = ReadTexts(texts);
        if (file.TryGet((uint)ChunkId.Grass, out Chunk grass))
            document.Grass = ReadGrass(grass);
        return document;
    }

    /// <summary>Every string a chunk stores is indexed from this table.</summary>
    private static void IndexStrings(LevelDocument document, StringTable strings)
    {
        strings.Add(document.Id);
        strings.Add(document.Slug);
        strings.Add(document.SongId);
        strings.Add(document.Name);
        strings.Add(document.BackgroundColor);
        strings.Add(document.OutsideColor);
        strings.Add(document.FrameColor);
        foreach (NoteDocument note in document.Notes)
            IndexItemStrings(note.Item, strings);
        foreach (StartPointDocument start in document.StartPoints)
            strings.Add(start.Name);
        foreach (GroupDocument group in document.Groups)
        {
            strings.Add(group.Name);
            foreach (GroupEffectDocument effect in group.Effects)
                strings.Add(effect.Tint);
        }
        foreach (ShaderDocument shader in document.Shaders)
        {
            strings.Add(shader.Name);
            strings.Add(shader.Source);
            foreach (string key in shader.Params.Keys)
                strings.Add(key);
        }
    }

    private static byte[] WriteMeta(LevelDocument document, StringTable strings)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(document.Version);
        strings.WriteIndex(writer, document.Id);
        strings.WriteIndex(writer, document.Slug);
        strings.WriteIndex(writer, document.SongId);
        strings.WriteIndex(writer, document.Name);
        writer.Write(document.SnapDivision);
        writer.Write(document.PlayfieldWidth);
        writer.Write(document.PlayfieldHeight);
        writer.Write(document.PlayerShader);
        writer.Write(document.FieldShader);
        // Appended after the original fields so maps written before backgrounds
        // existed still read, and older readers simply ignore the extra index.
        strings.WriteIndex(writer, document.BackgroundColor);
        // The outside and frame colours came later still, and trail the same way.
        strings.WriteIndex(writer, document.OutsideColor);
        strings.WriteIndex(writer, document.FrameColor);
        writer.Flush();
        return stream.ToArray();
    }

    private static LevelDocument ReadMeta(Chunk chunk, StringTable strings)
    {
        using BinaryReader reader = chunk.OpenReader();
        var document = new LevelDocument
        {
            Version = reader.ReadInt32(),
            Id = strings.ReadIndex(reader),
            Slug = strings.ReadIndex(reader),
            SongId = strings.ReadIndex(reader),
            Name = strings.ReadIndex(reader),
            SnapDivision = reader.ReadInt32(),
            PlayfieldWidth = reader.ReadInt32(),
            PlayfieldHeight = reader.ReadInt32(),
            PlayerShader = reader.ReadInt32(),
            FieldShader = reader.ReadInt32(),
        };

        if (reader.BaseStream.Length - reader.BaseStream.Position >= sizeof(uint))
            document.BackgroundColor = strings.ReadIndex(reader);
        if (reader.BaseStream.Length - reader.BaseStream.Position >= sizeof(uint))
            document.OutsideColor = strings.ReadIndex(reader);
        if (reader.BaseStream.Length - reader.BaseStream.Position >= sizeof(uint))
            document.FrameColor = strings.ReadIndex(reader);

        return document;
    }

    private static void WriteGroups(BinaryWriter writer, List<int> groups)
    {
        writer.Write((uint)groups.Count);
        foreach (int group in groups)
            writer.Write(group);
    }

    private static List<int> ReadGroups(BinaryReader reader)
    {
        uint count = ReadCount(reader, GroupLimits.Count, "group membership");
        var groups = new List<int>((int)count);
        for (uint i = 0; i < count; i++)
        {
            int group = reader.ReadInt32();
            if (group is < 0 or >= GroupLimits.Count)
                throw new FormatException($"XMF group {group} is out of range.");
            groups.Add(group);
        }
        return groups;
    }

    private static uint ReadCount(BinaryReader reader, uint max, string what)
    {
        uint count = reader.ReadUInt32();
        if (count > max)
            throw new FormatException($"XMF {what} count is too large.");
        return count;
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

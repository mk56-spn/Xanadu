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
    public const ushort MajorVersion = 1;
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

        /// <summary>Optional, so files written before spikes existed still load.</summary>
        Spikes = 9,
    }

    public static void Save(string path, LevelDocument document)
    {
        byte[] data = Write(document);
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllBytes(path, data);
    }

    public static LevelDocument Load(string path) =>
        Read(File.ReadAllBytes(path));

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
        // Omitted when empty so a level without spikes is byte-identical to one
        // written before spikes existed.
        if (document.Spikes.Count > 0)
            file.Add((uint)ChunkId.Spikes, WriteSpikes(document.Spikes));
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
        document.Mines = ReadMines(file.Get((uint)ChunkId.Mines, Format));
        document.Blocks = ReadBlocks(file.Get((uint)ChunkId.Blocks, Format));
        document.StartPoints = ReadStartPoints(
            file.Get((uint)ChunkId.StartPoints, Format),
            strings);
        document.Groups = ReadGroups(file.Get((uint)ChunkId.Groups, Format), strings);
        document.Shaders = ReadShaders(file.Get((uint)ChunkId.Shaders, Format), strings);
        document.Spikes = file.TryGet((uint)ChunkId.Spikes, out Chunk spikes)
            ? ReadSpikes(spikes)
            : new List<SpikeDocument>();
        return document;
    }

    /// <summary>Every string a chunk stores is indexed from this table.</summary>
    private static void IndexStrings(LevelDocument document, StringTable strings)
    {
        strings.Add(document.Id);
        strings.Add(document.Slug);
        strings.Add(document.SongId);
        strings.Add(document.Name);
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
        writer.Flush();
        return stream.ToArray();
    }

    private static LevelDocument ReadMeta(Chunk chunk, StringTable strings)
    {
        using BinaryReader reader = chunk.OpenReader();
        return new LevelDocument
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

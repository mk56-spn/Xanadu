using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Xanadu.Storage;

/// <summary>
/// Lays out the chunked files XAF character packages and XMF level maps share:
/// a magic number, a major/minor version and a directory of typed chunks. Each
/// format owns its chunk payloads; this only writes the frame around them.
/// </summary>
public sealed class ChunkWriter
{
    private const int DirectoryEntrySize = 12;

    private readonly byte[] _magic;
    private readonly ushort _major;
    private readonly ushort _minor;
    private readonly List<(uint Id, byte[] Data)> _chunks = new();

    public ChunkWriter(byte[] magic, ushort major, ushort minor)
    {
        _magic = magic;
        _major = major;
        _minor = minor;
    }

    public void Add(uint id, byte[] data) => _chunks.Add((id, data));

    public byte[] ToArray()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(_magic);
        writer.Write(_major);
        writer.Write(_minor);
        writer.Write((uint)_chunks.Count);

        int directoryStart = (int)stream.Position;
        writer.Write(new byte[_chunks.Count * DirectoryEntrySize]);

        var positions = new (uint Offset, uint Length)[_chunks.Count];
        for (int i = 0; i < _chunks.Count; i++)
        {
            positions[i] = ((uint)stream.Position, (uint)_chunks[i].Data.Length);
            writer.Write(_chunks[i].Data);
        }

        stream.Position = directoryStart;
        for (int i = 0; i < _chunks.Count; i++)
        {
            writer.Write(_chunks[i].Id);
            writer.Write(positions[i].Offset);
            writer.Write(positions[i].Length);
        }

        writer.Flush();
        return stream.ToArray();
    }
}

/// <summary>A slice of a chunked file, read with <see cref="OpenReader"/>.</summary>
public readonly struct Chunk(byte[] data, int offset, int length)
{
    public int Length => length;

    public BinaryReader OpenReader() =>
        new(new MemoryStream(data, offset, length, writable: false), Encoding.UTF8);

    public byte[] ToArray()
    {
        var copy = new byte[length];
        Array.Copy(data, offset, copy, 0, length);
        return copy;
    }
}

/// <summary>Reads the frame written by <see cref="ChunkWriter"/>.</summary>
public sealed class ChunkReader
{
    private const uint MaxChunks = 64;

    private readonly Dictionary<uint, Chunk> _chunks;

    private ChunkReader(Dictionary<uint, Chunk> chunks, ushort minor)
    {
        _chunks = chunks;
        Minor = minor;
    }

    public ushort Minor { get; }

    /// <summary>
    /// Opens a chunked file, checking its magic, major version and that every
    /// directory entry points inside the file. <paramref name="format"/> names
    /// the format in error messages, for example "XAF" or "XMF".
    /// </summary>
    public static ChunkReader Open(
        byte[] data,
        byte[] magic,
        ushort expectedMajor,
        string format)
    {
        using var stream = new MemoryStream(data, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8);

        byte[] found = reader.ReadBytes(magic.Length);
        if (found.Length != magic.Length || !found.AsSpan().SequenceEqual(magic))
            throw new FormatException($"Not an {format} file.");

        ushort major = reader.ReadUInt16();
        ushort minor = reader.ReadUInt16();
        if (major != expectedMajor)
            throw new FormatException($"Unsupported {format} major version {major}.");

        uint chunkCount = reader.ReadUInt32();
        if (chunkCount > MaxChunks)
            throw new FormatException($"Too many {format} chunks.");

        var chunks = new Dictionary<uint, Chunk>();
        for (uint i = 0; i < chunkCount; i++)
        {
            uint id = reader.ReadUInt32();
            uint offset = reader.ReadUInt32();
            uint length = reader.ReadUInt32();
            if (offset > (uint)data.Length || length > (uint)data.Length - offset)
                throw new FormatException($"{format} chunk outside file bounds.");
            chunks[id] = new Chunk(data, (int)offset, (int)length);
        }

        return new ChunkReader(chunks, minor);
    }

    public bool TryGet(uint id, out Chunk chunk) => _chunks.TryGetValue(id, out chunk);

    public Chunk Get(uint id, string format) =>
        _chunks.TryGetValue(id, out Chunk chunk)
            ? chunk
            : throw new FormatException($"{format} file is missing a required chunk.");
}

/// <summary>
/// The interned string table both formats put in their first chunk. Indices
/// are written into chunk payloads instead of the strings themselves.
/// </summary>
public sealed class StringTable(string format)
{
    private const uint MaxStrings = 100_000;

    private readonly Dictionary<string, int> _indices = new(StringComparer.Ordinal);

    public List<string> Values { get; } = new();

    public void Add(string value)
    {
        if (_indices.ContainsKey(value))
            return;
        _indices[value] = Values.Count;
        Values.Add(value);
    }

    public int GetIndex(string value)
    {
        Add(value);
        return _indices[value];
    }

    public string this[uint index] =>
        index < (uint)Values.Count
            ? Values[(int)index]
            : throw new FormatException($"{format} string index out of range.");

    public void WriteIndex(BinaryWriter writer, string value) =>
        writer.Write((uint)GetIndex(value));

    public string ReadIndex(BinaryReader reader) => this[reader.ReadUInt32()];

    public byte[] Write()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)Values.Count);
        foreach (string value in Values)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            writer.Write((uint)bytes.Length);
            writer.Write(bytes);
        }
        writer.Flush();
        return stream.ToArray();
    }

    public static StringTable Read(Chunk chunk, string format)
    {
        using BinaryReader reader = chunk.OpenReader();
        uint count = reader.ReadUInt32();
        if (count > MaxStrings)
            throw new FormatException($"{format} string table is too large.");

        var table = new StringTable(format);
        for (uint i = 0; i < count; i++)
        {
            uint length = reader.ReadUInt32();
            if (length > int.MaxValue ||
                length > reader.BaseStream.Length - reader.BaseStream.Position)
            {
                throw new FormatException($"Invalid {format} string length.");
            }

            string value = Encoding.UTF8.GetString(reader.ReadBytes((int)length));
            table._indices.TryAdd(value, table.Values.Count);
            table.Values.Add(value);
        }
        return table;
    }
}

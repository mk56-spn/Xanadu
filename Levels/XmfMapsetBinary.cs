using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xanadu.Ecs;
using Xanadu.Storage;

namespace Xanadu.Levels;

/// <summary>
/// Binary XMF2 mapset format: a mapset's own metadata and shared shaders,
/// plus each variant's chart, nested as self-contained blobs. Each variant
/// blob reuses the XMF1 chart chunk writers/readers, so a variant's own
/// parts round-trip exactly as a standalone level's do.
/// </summary>
public static partial class XmfBinary
{
    public const ushort MapsetMajorVersion = 1;
    public const ushort MapsetMinorVersion = 0;

    private const string MapsetFormat = "XMF2";

    private static readonly byte[] MapsetMagic = "XMF2"u8.ToArray();

    private enum MapsetChunkId : uint
    {
        Strings = 1,
        Meta = 2,
        Shaders = 3,
        Variants = 4,

        /// <summary>Optional pair/momentum data, absent in older mapsets.</summary>
        NoteExtras = 5,

        /// <summary>Optional Form shape references on blocks.</summary>
        BlockExtras = 6,
    }

    public static void SaveMapset(string path, XmfMapset mapset)
    {
        byte[] data = WriteMapset(mapset);
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllBytes(path, data);
    }

    public static XmfMapset LoadMapset(string path) =>
        ReadMapset(File.ReadAllBytes(path));

    public static byte[] WriteMapset(XmfMapset mapset)
    {
        var strings = new StringTable(MapsetFormat);
        IndexMapsetStrings(mapset, strings);

        var file = new ChunkWriter(MapsetMagic, MapsetMajorVersion, MapsetMinorVersion);
        file.Add((uint)MapsetChunkId.Strings, strings.Write());
        file.Add((uint)MapsetChunkId.Meta, WriteMapsetMeta(mapset, strings));
        file.Add((uint)MapsetChunkId.Shaders, WriteShaders(mapset.Shaders, strings));
        file.Add((uint)MapsetChunkId.Variants, WriteVariants(mapset.Variants, strings));
        if (mapset.Variants.Exists(variant => HasNoteExtras(variant.Notes)))
            file.Add((uint)MapsetChunkId.NoteExtras, WriteVariantNoteExtras(mapset.Variants));
        if (mapset.Variants.Exists(variant => HasBlockExtras(variant.Blocks)))
            file.Add((uint)MapsetChunkId.BlockExtras, WriteVariantBlockExtras(mapset.Variants));
        return file.ToArray();
    }

    public static XmfMapset ReadMapset(byte[] data)
    {
        try
        {
            return ReadMapsetData(data);
        }
        catch (Exception exception) when (exception is FormatException or IOException)
        {
            throw new XmfFormatException(exception.Message);
        }
    }

    private static XmfMapset ReadMapsetData(byte[] data)
    {
        ChunkReader file = ChunkReader.Open(data, MapsetMagic, MapsetMajorVersion, MapsetFormat);
        StringTable strings = StringTable.Read(
            file.Get((uint)MapsetChunkId.Strings, MapsetFormat),
            MapsetFormat);

        XmfMapset mapset = ReadMapsetMeta(file.Get((uint)MapsetChunkId.Meta, MapsetFormat), strings);
        mapset.Shaders = ReadShaders(file.Get((uint)MapsetChunkId.Shaders, MapsetFormat), strings);
        mapset.Variants = ReadVariants(file.Get((uint)MapsetChunkId.Variants, MapsetFormat), strings);
        if (file.TryGet((uint)MapsetChunkId.NoteExtras, out Chunk noteExtras))
            ReadVariantNoteExtras(noteExtras, mapset.Variants);
        if (file.TryGet((uint)MapsetChunkId.BlockExtras, out Chunk blockExtras))
            ReadVariantBlockExtras(blockExtras, mapset.Variants);
        return mapset;
    }

    private static void IndexMapsetStrings(XmfMapset mapset, StringTable strings)
    {
        strings.Add(mapset.Id);
        strings.Add(mapset.Slug);
        strings.Add(mapset.SongId);
        foreach (ShaderDocument shader in mapset.Shaders)
        {
            strings.Add(shader.Name);
            strings.Add(shader.Source);
            foreach (string key in shader.Params.Keys)
                strings.Add(key);
        }
        foreach (XmfVariant variant in mapset.Variants)
            IndexVariantStrings(variant, strings);
    }

    private static void IndexVariantStrings(XmfVariant variant, StringTable strings)
    {
        strings.Add(variant.Id);
        strings.Add(variant.Slug);
        strings.Add(variant.Name);
        strings.Add(variant.BackgroundColor);
        foreach (NoteDocument note in variant.Notes)
            IndexItemStrings(note.Item, strings);
        foreach (StartPointDocument start in variant.StartPoints)
            strings.Add(start.Name);
        foreach (GroupDocument group in variant.Groups)
        {
            strings.Add(group.Name);
            foreach (GroupEffectDocument effect in group.Effects)
                strings.Add(effect.Tint);
        }
    }

    private static byte[] WriteMapsetMeta(XmfMapset mapset, StringTable strings)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(mapset.Version);
        strings.WriteIndex(writer, mapset.Id);
        strings.WriteIndex(writer, mapset.Slug);
        strings.WriteIndex(writer, mapset.SongId);
        writer.Flush();
        return stream.ToArray();
    }

    private static XmfMapset ReadMapsetMeta(Chunk chunk, StringTable strings)
    {
        using BinaryReader reader = chunk.OpenReader();
        return new XmfMapset
        {
            Version = reader.ReadInt32(),
            Id = strings.ReadIndex(reader),
            Slug = strings.ReadIndex(reader),
            SongId = strings.ReadIndex(reader),
        };
    }

    private static byte[] WriteVariants(List<XmfVariant> variants, StringTable strings)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)variants.Count);
        foreach (XmfVariant variant in variants)
            WriteBlob(writer, WriteVariant(variant, strings));
        writer.Flush();
        return stream.ToArray();
    }

    private static List<XmfVariant> ReadVariants(Chunk chunk, StringTable strings)
    {
        using BinaryReader reader = chunk.OpenReader();
        uint count = ReadCount(reader, MaxItems, "variant");
        var variants = new List<XmfVariant>((int)count);
        for (uint i = 0; i < count; i++)
            variants.Add(ReadVariant(ReadBlob(reader), strings));
        return variants;
    }

    private static byte[] WriteVariant(XmfVariant variant, StringTable strings)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        strings.WriteIndex(writer, variant.Id);
        strings.WriteIndex(writer, variant.Slug);
        strings.WriteIndex(writer, variant.Name);
        writer.Write(variant.SnapDivision);
        writer.Write(variant.PlayfieldWidth);
        writer.Write(variant.PlayfieldHeight);
        writer.Write(variant.PlayerShader);
        writer.Write(variant.FieldShader);
        WriteBlob(writer, WriteNotes(variant.Notes, strings));
        WriteBlob(writer, WriteMines(variant.Mines));
        WriteBlob(writer, WriteBlocks(variant.Blocks));
        WriteBlob(writer, WriteStartPoints(variant.StartPoints, strings));
        WriteBlob(writer, WriteGroups(variant.Groups, strings));
        WriteBlob(writer, WriteSpikes(variant.Spikes));
        // The background comes last so a variant written before backgrounds
        // existed still reads; older readers ignore the trailing index.
        strings.WriteIndex(writer, variant.BackgroundColor);
        writer.Flush();
        return stream.ToArray();
    }

    private static XmfVariant ReadVariant(byte[] data, StringTable strings)
    {
        using var stream = new MemoryStream(data, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8);
        var variant = new XmfVariant
        {
            Id = strings.ReadIndex(reader),
            Slug = strings.ReadIndex(reader),
            Name = strings.ReadIndex(reader),
            SnapDivision = reader.ReadInt32(),
            PlayfieldWidth = reader.ReadInt32(),
            PlayfieldHeight = reader.ReadInt32(),
            PlayerShader = reader.ReadInt32(),
            FieldShader = reader.ReadInt32(),
        };

        variant.Notes = ReadNotes(ReadBlobChunk(reader), strings);
        variant.Mines = ReadMines(ReadBlobChunk(reader));
        variant.Blocks = ReadBlocks(ReadBlobChunk(reader));
        variant.StartPoints = ReadStartPoints(ReadBlobChunk(reader), strings);
        variant.Groups = ReadGroups(ReadBlobChunk(reader), strings);
        variant.Spikes = ReadSpikes(ReadBlobChunk(reader));
        if (stream.Length - stream.Position >= sizeof(uint))
            variant.BackgroundColor = strings.ReadIndex(reader);

        return variant;
    }

    private static byte[] WriteVariantNoteExtras(List<XmfVariant> variants)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)variants.Count);
        foreach (XmfVariant variant in variants)
        {
            writer.Write((uint)variant.Notes.Count);
            foreach (NoteDocument note in variant.Notes)
            {
                writer.Write((byte)note.PairIndex);
                writer.Write(note.Momentum);
                writer.Write(note.ContactLeft);
                writer.Write(note.ContactRight);
                writer.Write((byte)note.ChannelSide);
                writer.Write(note.HoldMs);
                writer.Write(note.ShapeId);
                writer.Write(note.ShapePiece);
            }
        }
        writer.Flush();
        return stream.ToArray();
    }

    private static void ReadVariantNoteExtras(
        Chunk chunk,
        List<XmfVariant> variants)
    {
        using BinaryReader reader = chunk.OpenReader();
        uint variantCount = ReadCount(reader, MaxItems, "variant note extra");
        if (variantCount != variants.Count)
            throw new FormatException("XMF variant note extras do not match the variant count.");

        foreach (XmfVariant variant in variants)
        {
            uint noteCount = ReadCount(reader, MaxItems, "note extra");
            if (noteCount != variant.Notes.Count)
                throw new FormatException("XMF note extras do not match the note count.");

            for (int i = 0; i < variant.Notes.Count; i++)
            {
                PairIndex pairIndex = ReadEnum<PairIndex>(reader);
                float momentum = reader.ReadSingle();
                int contactLeft = reader.ReadInt32();
                int contactRight = reader.ReadInt32();
                ChannelSide channelSide = ReadEnum<ChannelSide>(reader);
                long holdMs = reader.ReadInt64();
                int shapeId = reader.ReadInt32();
                int shapePiece = reader.ReadInt32();
                if (!float.IsFinite(momentum) || momentum < 0f)
                    throw new FormatException("XMF momentum note has an invalid magnitude.");
                if (contactLeft < 0 || contactRight < 0)
                    throw new FormatException("XMF contact barrier has a negative charge cost.");
                if (holdMs < 0)
                    throw new FormatException("XMF rail hold has a negative length.");
                if (shapeId < 0 || shapePiece < 0)
                    throw new FormatException("XMF construction note has a negative shape reference.");

                variant.Notes[i].PairIndex = pairIndex;
                variant.Notes[i].Momentum = momentum;
                variant.Notes[i].ContactLeft = contactLeft;
                variant.Notes[i].ContactRight = contactRight;
                variant.Notes[i].ChannelSide = channelSide;
                variant.Notes[i].HoldMs = holdMs;
                variant.Notes[i].ShapeId = shapeId;
                variant.Notes[i].ShapePiece = shapePiece;
            }
        }
    }

    private static byte[] WriteVariantBlockExtras(List<XmfVariant> variants)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)variants.Count);
        foreach (XmfVariant variant in variants)
        {
            writer.Write((uint)variant.Blocks.Count);
            foreach (BlockDocument block in variant.Blocks)
            {
                writer.Write(block.ShapeId);
                writer.Write(block.ShapePiece);
            }
        }
        writer.Flush();
        return stream.ToArray();
    }

    private static void ReadVariantBlockExtras(
        Chunk chunk,
        List<XmfVariant> variants)
    {
        using BinaryReader reader = chunk.OpenReader();
        uint variantCount = ReadCount(reader, MaxItems, "variant block extra");
        if (variantCount != variants.Count)
            throw new FormatException("XMF variant block extras do not match the variant count.");

        foreach (XmfVariant variant in variants)
        {
            uint blockCount = ReadCount(reader, MaxItems, "block extra");
            if (blockCount != variant.Blocks.Count)
                throw new FormatException("XMF block extras do not match the block count.");

            for (int i = 0; i < variant.Blocks.Count; i++)
            {
                int shapeId = reader.ReadInt32();
                int shapePiece = reader.ReadInt32();
                if (shapeId < 0 || shapePiece < 0)
                    throw new FormatException("XMF block has a negative Form shape reference.");
                variant.Blocks[i].ShapeId = shapeId;
                variant.Blocks[i].ShapePiece = shapePiece;
            }
        }
    }

    private static void WriteBlob(BinaryWriter writer, byte[] blob)
    {
        writer.Write((uint)blob.Length);
        writer.Write(blob);
    }

    private static byte[] ReadBlob(BinaryReader reader)
    {
        uint length = reader.ReadUInt32();
        return reader.ReadBytes((int)length);
    }

    private static Chunk ReadBlobChunk(BinaryReader reader)
    {
        byte[] blob = ReadBlob(reader);
        return new Chunk(blob, 0, blob.Length);
    }
}

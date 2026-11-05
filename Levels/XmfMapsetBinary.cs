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
    public const ushort MapsetMajorVersion = 2;
    public const ushort MapsetMinorVersion = 6;

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

        /// <summary>Optional item colours, absent when every item is untinted.</summary>
        SelfModulates = 7,

        /// <summary>Each variant's spike outlines, absent in mapsets from before spikes had them.</summary>
        SpikeOutlines = 8,

        /// <summary>Each variant's text, absent when no variant has any.</summary>
        Texts = 9,

        /// <summary>Each variant's grass, absent when no variant has any.</summary>
        Grass = 10,

        /// <summary>Each variant's filled shapes, absent when no variant has any.</summary>
        Polygons = 11,

        /// <summary>Each variant's item layers, absent when every item is on its default layer at z 0.</summary>
        ItemLayers = 12,

        /// <summary>Each variant's layer table, absent when no variant has a layer with settings.</summary>
        LayerTable = 13,

        /// <summary>Each variant's item blends, absent when every item is on mix.</summary>
        ItemBlends = 14,

        /// <summary>Each variant's group shader tracks, absent when no group has one.</summary>
        GroupShaders = 15,

        /// <summary>Each variant's item shader sources, absent when every item uses its own.</summary>
        ItemShaderSources = 16,

        /// <summary>Each variant's buffered groups, absent when none is buffered.</summary>
        GroupBuffers = 17,

        /// <summary>Each variant's screen-read regions, absent when none has any.</summary>
        ScreenReads = 18,
    }

    public static void SaveMapset(string path, XmfMapset mapset)
    {
        BinaryFile.Save(path, WriteMapset(mapset));
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
        file.Add((uint)MapsetChunkId.Shaders, XmfChromeChunks.WriteShaders(mapset.Shaders, strings));
        file.Add((uint)MapsetChunkId.Variants, WriteVariants(mapset.Variants, strings));
        if (mapset.Variants.Exists(variant => XmfNoteChunks.HasNoteExtras(variant.Notes)))
            file.Add((uint)MapsetChunkId.NoteExtras, WriteVariantNoteExtras(mapset.Variants));
        if (mapset.Variants.Exists(XmfItemColors.HasSelfModulates))
            file.Add((uint)MapsetChunkId.SelfModulates, WriteVariantSelfModulates(mapset.Variants));
        file.Add((uint)MapsetChunkId.SpikeOutlines, WriteVariantSpikeOutlines(mapset.Variants));
        if (mapset.Variants.Exists(HasTexts))
            file.Add((uint)MapsetChunkId.Texts, WriteVariantTexts(mapset.Variants));
        if (mapset.Variants.Exists(HasGrass))
            file.Add((uint)MapsetChunkId.Grass, WriteVariantGrass(mapset.Variants));
        if (mapset.Variants.Exists(HasPolygons))
            file.Add((uint)MapsetChunkId.Polygons, WriteVariantPolygons(mapset.Variants, strings));
        if (mapset.Variants.Exists(XmfItemLayers.HasLayers))
            file.Add((uint)MapsetChunkId.ItemLayers, WriteVariantLayers(mapset.Variants));
        if (mapset.Variants.Exists(XmfLayerTable.HasLayers))
            file.Add((uint)MapsetChunkId.LayerTable, WriteVariantLayerTables(mapset.Variants));
        if (mapset.Variants.Exists(XmfItemBlends.HasBlends))
            file.Add((uint)MapsetChunkId.ItemBlends, WriteVariantBlends(mapset.Variants));
        if (mapset.Variants.Exists(XmfGroupShaders.HasTracks))
            file.Add((uint)MapsetChunkId.GroupShaders, WriteVariantGroupShaders(mapset.Variants));
        if (mapset.Variants.Exists(XmfItemShaderSources.HasSources))
            file.Add((uint)MapsetChunkId.ItemShaderSources, WriteVariantShaderSources(mapset.Variants));
        if (mapset.Variants.Exists(XmfGroupBuffers.HasBuffers))
            file.Add((uint)MapsetChunkId.GroupBuffers, WriteVariantGroupBuffers(mapset.Variants));
        if (mapset.Variants.Exists(XmfBinary.HasScreenReads))
            file.Add((uint)MapsetChunkId.ScreenReads, WriteVariantScreenReads(mapset.Variants));
        return file.ToArray();
    }

    public static XmfMapset ReadMapset(byte[] data) => XmfBinary.Guard(() => ReadMapsetData(data));

    private static XmfMapset ReadMapsetData(byte[] data)
    {
        ChunkReader file = ChunkReader.Open(data, MapsetMagic, MapsetMajorVersion, MapsetFormat);
        StringTable strings = StringTable.Read(
            file.Get((uint)MapsetChunkId.Strings, MapsetFormat),
            MapsetFormat);

        XmfMapset mapset = ReadMapsetMeta(file.Get((uint)MapsetChunkId.Meta, MapsetFormat), strings);
        mapset.Shaders = XmfChromeChunks.ReadShaders(file.Get((uint)MapsetChunkId.Shaders, MapsetFormat), strings);
        mapset.Variants = ReadVariants(file.Get((uint)MapsetChunkId.Variants, MapsetFormat), strings);
        if (file.TryGet((uint)MapsetChunkId.NoteExtras, out Chunk noteExtras))
            ReadVariantNoteExtras(noteExtras, mapset.Variants);
        if (file.TryGet((uint)MapsetChunkId.SelfModulates, out Chunk selfModulates))
            ReadVariantSelfModulates(selfModulates, mapset.Variants);
        if (file.TryGet((uint)MapsetChunkId.SpikeOutlines, out Chunk spikeOutlines))
            ReadVariantSpikeOutlines(spikeOutlines, mapset.Variants);
        if (file.TryGet((uint)MapsetChunkId.Texts, out Chunk texts))
            ReadVariantTexts(texts, mapset.Variants);
        if (file.TryGet((uint)MapsetChunkId.Grass, out Chunk grass))
            ReadVariantGrass(grass, mapset.Variants);
        if (file.TryGet((uint)MapsetChunkId.Polygons, out Chunk polygons))
            ReadVariantPolygons(polygons, mapset.Variants, strings);
        // Before the item layers, which list each kind's items in this order.
        if (file.TryGet((uint)MapsetChunkId.ScreenReads, out Chunk variantScreenReads))
            ReadVariantScreenReads(variantScreenReads, mapset.Variants);
        if (file.TryGet((uint)MapsetChunkId.ItemLayers, out Chunk layers))
            ReadVariantLayers(layers, mapset.Variants);
        if (file.TryGet((uint)MapsetChunkId.LayerTable, out Chunk tables))
            ReadVariantLayerTables(tables, mapset.Variants);
        if (file.TryGet((uint)MapsetChunkId.ItemBlends, out Chunk blends))
            ReadVariantBlends(blends, mapset.Variants);
        if (file.TryGet((uint)MapsetChunkId.GroupShaders, out Chunk groupShaders))
            ReadVariantGroupShaders(groupShaders, mapset.Variants);
        if (file.TryGet((uint)MapsetChunkId.ItemShaderSources, out Chunk sources))
            ReadVariantShaderSources(sources, mapset.Variants);
        if (file.TryGet((uint)MapsetChunkId.GroupBuffers, out Chunk groupBuffers))
            ReadVariantGroupBuffers(groupBuffers, mapset.Variants);
        return mapset;
    }

    private static byte[] WriteVariantTexts(List<XmfVariant> variants)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)variants.Count);
        foreach (XmfVariant variant in variants)
            WriteTexts(writer, variant.Texts);
        foreach (XmfVariant variant in variants)
            WriteTextRotations(writer, variant.Texts);
        writer.Flush();
        return stream.ToArray();
    }

    private static void ReadVariantTexts(Chunk chunk, List<XmfVariant> variants)
    {
        using BinaryReader reader = chunk.OpenReader();
        uint count = ReadCount(reader, XmfItemIO.MaxItems, "variant text");
        if (count != variants.Count)
            throw new FormatException("XMF variant texts do not match the variant count.");
        foreach (XmfVariant variant in variants)
            variant.Texts = ReadTexts(reader);
        foreach (XmfVariant variant in variants)
            ReadTextRotations(reader, variant.Texts);
    }

    private static byte[] WriteVariantScreenReads(List<XmfVariant> variants)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)variants.Count);
        foreach (XmfVariant variant in variants)
            WriteScreenReads(writer, variant.ScreenReads);
        writer.Flush();
        return stream.ToArray();
    }

    private static void ReadVariantScreenReads(Chunk chunk, List<XmfVariant> variants)
    {
        using BinaryReader reader = chunk.OpenReader();
        uint count = ReadCount(reader, XmfItemIO.MaxItems, "variant screen-read region");
        if (count != variants.Count)
            throw new FormatException("XMF variant screen reads do not match the variant count.");
        foreach (XmfVariant variant in variants)
            variant.ScreenReads = ReadScreenReads(reader);
    }

    private static byte[] WriteVariantSpikeOutlines(List<XmfVariant> variants)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)variants.Count);
        foreach (XmfVariant variant in variants)
            writer.Write(WriteSpikeOutlines(variant.Spikes));
        writer.Flush();
        return stream.ToArray();
    }

    private static void ReadVariantSpikeOutlines(Chunk chunk, List<XmfVariant> variants)
    {
        using BinaryReader reader = chunk.OpenReader();
        uint count = ReadCount(reader, XmfItemIO.MaxItems, "variant spike outline");
        if (count != variants.Count)
            throw new FormatException("XMF variant spike outlines do not match the variant count.");
        foreach (XmfVariant variant in variants)
            ReadSpikeOutlines(reader, variant.Spikes);
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
        strings.Add(variant.OutsideColor);
        strings.Add(variant.FrameColor);
        foreach (NoteDocument note in variant.Notes)
            XmfItemIO.IndexItemStrings(note.Item, strings);
        foreach (PolygonDocument polygon in variant.Polygons)
            XmfItemIO.IndexItemStrings(polygon.Item, strings);
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
        uint count = ReadCount(reader, XmfItemIO.MaxItems, "variant");
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
        WriteBlob(writer, XmfNoteChunks.WriteNotes(variant.Notes, strings));
        WriteBlob(writer, XmfBodyChunks.WriteMines(variant.Mines));
        WriteBlob(writer, XmfBodyChunks.WriteBlocks(variant.Blocks));
        WriteBlob(writer, XmfChromeChunks.WriteStartPoints(variant.StartPoints, strings));
        WriteBlob(writer, XmfChromeChunks.WriteGroupDefinitions(variant.Groups, strings));
        WriteBlob(writer, XmfBodyChunks.WriteSpikes(variant.Spikes));
        // The background comes last so a variant written before backgrounds
        // existed still reads; older readers ignore the trailing index.
        strings.WriteIndex(writer, variant.BackgroundColor);
        // The outside and frame colours came later still, and trail the same way.
        strings.WriteIndex(writer, variant.OutsideColor);
        strings.WriteIndex(writer, variant.FrameColor);
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

        variant.Notes = XmfNoteChunks.ReadNotes(ReadBlobChunk(reader), strings);
        variant.Mines = XmfBodyChunks.ReadMines(ReadBlobChunk(reader));
        variant.Blocks = XmfBodyChunks.ReadBlocks(ReadBlobChunk(reader));
        variant.StartPoints = XmfChromeChunks.ReadStartPoints(ReadBlobChunk(reader), strings);
        variant.Groups = XmfChromeChunks.ReadGroupDefinitions(ReadBlobChunk(reader), strings);
        variant.Spikes = XmfBodyChunks.ReadSpikes(ReadBlobChunk(reader));
        if (stream.Length - stream.Position >= sizeof(uint))
            variant.BackgroundColor = strings.ReadIndex(reader);
        if (stream.Length - stream.Position >= sizeof(uint))
            variant.OutsideColor = strings.ReadIndex(reader);
        if (stream.Length - stream.Position >= sizeof(uint))
            variant.FrameColor = strings.ReadIndex(reader);

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
        uint variantCount = ReadCount(reader, XmfItemIO.MaxItems, "variant note extra");
        if (variantCount != variants.Count)
            throw new FormatException("XMF variant note extras do not match the variant count.");

        foreach (XmfVariant variant in variants)
        {
            uint noteCount = ReadCount(reader, XmfItemIO.MaxItems, "note extra");
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

    private static byte[] WriteVariantSelfModulates(List<XmfVariant> variants)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)variants.Count);
        foreach (XmfVariant variant in variants)
            XmfItemColors.WriteSelfModulates(writer, variant);
        writer.Flush();
        return stream.ToArray();
    }

    private static void ReadVariantSelfModulates(Chunk chunk, List<XmfVariant> variants)
    {
        using BinaryReader reader = chunk.OpenReader();
        uint count = ReadCount(reader, XmfItemIO.MaxItems, "variant self modulate");
        if (count != variants.Count)
            throw new FormatException("XMF variant self modulates do not match the variant count.");
        foreach (XmfVariant variant in variants)
            XmfItemColors.ReadSelfModulates(reader, variant);
    }

    private static byte[] WriteVariantLayers(List<XmfVariant> variants)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)variants.Count);
        foreach (XmfVariant variant in variants)
            XmfItemLayers.Write(writer, variant);
        writer.Flush();
        return stream.ToArray();
    }

    private static void ReadVariantLayers(Chunk chunk, List<XmfVariant> variants)
    {
        using BinaryReader reader = chunk.OpenReader();
        uint count = ReadCount(reader, XmfItemIO.MaxItems, "variant item layer");
        if (count != variants.Count)
            throw new FormatException("XMF variant item layers do not match the variant count.");
        foreach (XmfVariant variant in variants)
            XmfItemLayers.Read(reader, variant);
    }

    private static byte[] WriteVariantLayerTables(List<XmfVariant> variants)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)variants.Count);
        foreach (XmfVariant variant in variants)
            XmfLayerTable.Write(writer, variant);
        writer.Flush();
        return stream.ToArray();
    }

    private static void ReadVariantLayerTables(Chunk chunk, List<XmfVariant> variants)
    {
        using BinaryReader reader = chunk.OpenReader();
        uint count = ReadCount(reader, XmfItemIO.MaxItems, "variant layer table");
        if (count != variants.Count)
            throw new FormatException("XMF variant layer tables do not match the variant count.");
        foreach (XmfVariant variant in variants)
            XmfLayerTable.Read(reader, variant);
    }

    private static byte[] WriteVariantBlends(List<XmfVariant> variants)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)variants.Count);
        foreach (XmfVariant variant in variants)
            XmfItemBlends.Write(writer, variant);
        writer.Flush();
        return stream.ToArray();
    }

    private static void ReadVariantBlends(Chunk chunk, List<XmfVariant> variants)
    {
        using BinaryReader reader = chunk.OpenReader();
        uint count = ReadCount(reader, XmfItemIO.MaxItems, "variant item blend");
        if (count != variants.Count)
            throw new FormatException("XMF variant item blends do not match the variant count.");
        foreach (XmfVariant variant in variants)
            XmfItemBlends.Read(reader, variant);
    }

    private static byte[] WriteVariantGroupShaders(List<XmfVariant> variants)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)variants.Count);
        foreach (XmfVariant variant in variants)
            XmfGroupShaders.Write(writer, variant);
        writer.Flush();
        return stream.ToArray();
    }

    private static void ReadVariantGroupShaders(Chunk chunk, List<XmfVariant> variants)
    {
        using BinaryReader reader = chunk.OpenReader();
        uint count = ReadCount(reader, XmfItemIO.MaxItems, "variant group shader track");
        if (count != variants.Count)
            throw new FormatException("XMF variant group shader tracks do not match the variant count.");
        foreach (XmfVariant variant in variants)
            XmfGroupShaders.Read(reader, variant);
    }

    private static byte[] WriteVariantShaderSources(List<XmfVariant> variants)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)variants.Count);
        foreach (XmfVariant variant in variants)
            XmfItemShaderSources.Write(writer, variant);
        writer.Flush();
        return stream.ToArray();
    }

    private static void ReadVariantShaderSources(Chunk chunk, List<XmfVariant> variants)
    {
        using BinaryReader reader = chunk.OpenReader();
        uint count = ReadCount(reader, XmfItemIO.MaxItems, "variant item shader source");
        if (count != variants.Count)
            throw new FormatException("XMF variant item shader sources do not match the variant count.");
        foreach (XmfVariant variant in variants)
            XmfItemShaderSources.Read(reader, variant);
    }

    private static byte[] WriteVariantGroupBuffers(List<XmfVariant> variants)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)variants.Count);
        foreach (XmfVariant variant in variants)
            XmfGroupBuffers.Write(writer, variant);
        writer.Flush();
        return stream.ToArray();
    }

    private static void ReadVariantGroupBuffers(Chunk chunk, List<XmfVariant> variants)
    {
        using BinaryReader reader = chunk.OpenReader();
        uint count = ReadCount(reader, XmfItemIO.MaxItems, "variant group buffer");
        if (count != variants.Count)
            throw new FormatException("XMF variant group buffers do not match the variant count.");
        foreach (XmfVariant variant in variants)
            XmfGroupBuffers.Read(reader, variant);
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

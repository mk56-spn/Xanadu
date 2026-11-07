using System.Collections.Generic;
using System.IO;
using System.Text;
using Xanadu.Storage;

namespace Xanadu.Levels;

/// <summary>The chunks of an XMF mapset file.</summary>
public enum XmfMapsetChunk : uint
{
    Strings = 1,
    Meta = 2,
    Shaders = 3,
    Variants = 4,
}

/// <summary>
/// Binary XMF2 mapset format: a mapset's own metadata and shared shaders,
/// then each variant as a whole XMF map file, carrying no song or shaders of
/// its own. A variant reads and writes exactly as a standalone level does, so
/// anything a level gains its variants gain with no change here.
/// </summary>
public static partial class XmfBinary
{
    public const ushort MapsetMajorVersion = 3;
    public const ushort MapsetMinorVersion = 0;

    private const string MapsetFormat = "XMF2";

    private static readonly byte[] MapsetMagic = "XMF2"u8.ToArray();

    /// <summary>More variants than any mapset should hold.</summary>
    private const uint MaxVariants = 4_096;

    public static void SaveMapset(string path, XmfMapset mapset)
    {
        BinaryFile.Save(path, WriteMapset(mapset));
    }

    public static XmfMapset LoadMapset(string path) =>
        ReadMapset(File.ReadAllBytes(path));

    public static byte[] WriteMapset(XmfMapset mapset)
    {
        var strings = new StringTable(MapsetFormat);
        byte[] meta = WriteMapsetMeta(mapset, strings);
        byte[] shaders = XmfRecords.WriteList(mapset.Shaders, (w, s) => XmfChrome.WriteShader(w, s, strings));

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((uint)mapset.Variants.Count);
            foreach (XmfVariant variant in mapset.Variants)
            {
                byte[] blob = Write(variant.ToDocument(mapset.Version, "", new List<ShaderDocument>()));
                writer.Write((uint)blob.Length);
                writer.Write(blob);
            }
        }

        var file = new ChunkWriter(MapsetMagic, MapsetMajorVersion, MapsetMinorVersion);
        file.Add((uint)XmfMapsetChunk.Strings, strings.Write());
        file.Add((uint)XmfMapsetChunk.Meta, meta);
        file.Add((uint)XmfMapsetChunk.Shaders, shaders);
        file.Add((uint)XmfMapsetChunk.Variants, stream.ToArray());
        return file.ToArray();
    }

    public static XmfMapset ReadMapset(byte[] data) => Guard(() => ReadMapsetData(data));

    private static XmfMapset ReadMapsetData(byte[] data)
    {
        ChunkReader file = ChunkReader.Open(data, MapsetMagic, MapsetMajorVersion, MapsetFormat);
        StringTable strings = StringTable.Read(file.Get((uint)XmfMapsetChunk.Strings, MapsetFormat), MapsetFormat);
        XmfMapset mapset = ReadMapsetMeta(file.Get((uint)XmfMapsetChunk.Meta, MapsetFormat), strings);
        mapset.Shaders = XmfRecords.ReadList(
            file.Get((uint)XmfMapsetChunk.Shaders, MapsetFormat),
            Xanadu.Ecs.ShaderLimits.Count,
            "shader",
            r => XmfChrome.ReadShader(r, strings));

        using BinaryReader reader = file.Get((uint)XmfMapsetChunk.Variants, MapsetFormat).OpenReader();
        uint count = ReadCount(reader, MaxVariants, "variant");
        for (uint i = 0; i < count; i++)
        {
            uint length = reader.ReadUInt32();
            if (length > reader.BaseStream.Length - reader.BaseStream.Position)
                throw new System.FormatException("XMF variant runs past its chunk.");
            mapset.Variants.Add(XmfVariant.From(ReadMap(reader.ReadBytes((int)length))));
        }

        return mapset;
    }

    private static byte[] WriteMapsetMeta(XmfMapset mapset, StringTable strings)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        XmfRecords.Write(writer, mapset, (w, m) =>
        {
            w.Write(m.Version);
            strings.WriteIndex(w, m.Id);
            strings.WriteIndex(w, m.Slug);
            strings.WriteIndex(w, m.SongId);
        });
        writer.Flush();
        return stream.ToArray();
    }

    private static XmfMapset ReadMapsetMeta(Chunk chunk, StringTable strings)
    {
        using BinaryReader reader = chunk.OpenReader();
        return XmfRecords.Read(reader, "mapset meta", r => new XmfMapset
        {
            Version = r.ReadInt32(),
            Id = strings.ReadIndex(r),
            Slug = strings.ReadIndex(r),
            SongId = strings.ReadIndex(r),
        });
    }
}

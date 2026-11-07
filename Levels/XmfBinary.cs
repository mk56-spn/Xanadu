using System;
using System.Collections.Generic;
using System.IO;
using Xanadu.Storage;

namespace Xanadu.Levels;

/// <summary>An XMF map file that could not be read.</summary>
public sealed class XmfFormatException(string message) : Exception(message);

/// <summary>The chunks of an XMF map file. Every chunk but the strings and meta is left out when it would be empty.</summary>
public enum XmfChunk : uint
{
    Strings = 1,
    Meta = 2,
    Shaders = 3,
    Groups = 4,
    Layers = 5,

    Notes = 10,
    Mines = 11,
    StartPoints = 12,
    Blocks = 13,
    Spikes = 14,
    Texts = 15,
    Grass = 16,
    Flares = 17,
    Polygons = 18,
    ScreenReads = 19,
}

/// <summary>
/// Binary XMF map format. It shares its container — magic, version, chunk
/// directory and string table — with XAF packages. Inside, every list is a
/// count of length-framed records (<see cref="XmfRecords"/>), and every item
/// record opens with the same header (groups, shader, shader source, layer, z,
/// blend, colour), then its kind's own fields. A new item kind is a new chunk;
/// a new field is appended to its record.
/// </summary>
public static partial class XmfBinary
{
    public const ushort MajorVersion = 3;
    public const ushort MinorVersion = 0;

    /// <summary>The extension level files are saved with, including the dot.</summary>
    public const string Extension = ".xmf";

    /// <summary>The longest text one item may hold, in characters.</summary>
    public const int MaxTextLength = 4_096;

    private const string Format = "XMF";

    private static readonly byte[] Magic = "XMF1"u8.ToArray();

    public static byte[] Write(LevelDocument document)
    {
        var strings = new StringTable(Format);
        var chunks = new List<(XmfChunk Id, byte[] Data)>
        {
            (XmfChunk.Meta, XmfChrome.WriteMeta(document, strings)),
        };

        void Add<T>(XmfChunk id, List<T>? list, Func<List<T>, byte[]> write)
        {
            if (list is { Count: > 0 })
                chunks.Add((id, write(list)));
        }

        Add(XmfChunk.Shaders, document.Shaders, list => XmfRecords.WriteList(list, (w, s) => XmfChrome.WriteShader(w, s, strings)));
        Add(XmfChunk.Groups, document.Groups, list => XmfRecords.WriteList(list, (w, g) => XmfChrome.WriteGroup(w, g, strings)));
        Add(XmfChunk.Layers, XmfChrome.Ordered(document.Layers), list => XmfRecords.WriteList(list, XmfChrome.WriteLayer));
        Add(XmfChunk.Notes, document.Notes, list => XmfRecords.WriteItems(list, (w, n) => XmfItems.WriteNote(w, n, strings)));
        Add(XmfChunk.Mines, document.Mines, list => XmfRecords.WriteItems(list, XmfItems.WriteMine));
        Add(XmfChunk.StartPoints, document.StartPoints, list => XmfRecords.WriteItems(list, (w, s) => XmfItems.WriteStartPoint(w, s, strings)));
        Add(XmfChunk.Blocks, document.Blocks, list => XmfRecords.WriteItems(list, XmfItems.WriteBlock));
        Add(XmfChunk.Spikes, document.Spikes, list => XmfRecords.WriteItems(list, XmfItems.WriteSpike));
        Add(XmfChunk.Texts, document.Texts, list => XmfRecords.WriteItems(list, XmfItems.WriteText));
        Add(XmfChunk.Grass, document.Grass, list => XmfRecords.WriteItems(list, XmfItems.WriteGrass));
        Add(XmfChunk.Flares, document.Flares, list => XmfRecords.WriteItems(list, XmfItems.WriteFlare));
        Add(XmfChunk.Polygons, document.Polygons, list => XmfRecords.WriteItems(list, (w, p) => XmfItems.WritePolygon(w, p, strings)));
        Add(XmfChunk.ScreenReads, document.ScreenReads, list => XmfRecords.WriteItems(list, XmfItems.WriteScreenRead));

        // The table is written last, once every chunk has put its strings in it.
        var file = new ChunkWriter(Magic, MajorVersion, MinorVersion);
        file.Add((uint)XmfChunk.Strings, strings.Write());
        foreach ((XmfChunk id, byte[] data) in chunks)
            file.Add((uint)id, data);
        return file.ToArray();
    }

    public static LevelDocument Read(byte[] data) => Guard(() => ReadMap(data));

    /// <summary>Runs a read and reports whatever went wrong as a format error, for both level and mapset files.</summary>
    internal static T Guard<T>(Func<T> read)
    {
        try
        {
            return read();
        }
        catch (Exception exception) when (exception is FormatException or IOException)
        {
            throw new XmfFormatException(exception.Message);
        }
    }

    private static LevelDocument ReadMap(byte[] data)
    {
        ChunkReader file = ChunkReader.Open(data, Magic, MajorVersion, Format);
        StringTable strings = StringTable.Read(file.Get((uint)XmfChunk.Strings, Format), Format);
        LevelDocument document = XmfChrome.ReadMeta(file.Get((uint)XmfChunk.Meta, Format), strings);

        List<T> List<T>(XmfChunk id, Func<Chunk, List<T>> read) =>
            file.TryGet((uint)id, out Chunk chunk) ? read(chunk) : new List<T>();

        document.Shaders = List(XmfChunk.Shaders, c => XmfRecords.ReadList(c, Xanadu.Ecs.ShaderLimits.Count, "shader", r => XmfChrome.ReadShader(r, strings)));
        document.Groups = List(XmfChunk.Groups, c => XmfRecords.ReadList(c, Xanadu.Ecs.GroupLimits.Count, "group", r => XmfChrome.ReadGroup(r, strings)));
        document.Layers = List(XmfChunk.Layers, c => XmfRecords.ReadList(c, XmfChrome.LayerLimit, "layer", XmfChrome.ReadLayer));
        document.Notes = List(XmfChunk.Notes, c => XmfRecords.ReadItems<NoteDocument>(c, "note", (r, n) => XmfItems.ReadNote(r, n, strings)));
        document.Mines = List(XmfChunk.Mines, c => XmfRecords.ReadItems<MineDocument>(c, "mine", XmfItems.ReadMine));
        document.StartPoints = List(XmfChunk.StartPoints, c => XmfRecords.ReadItems<StartPointDocument>(c, "start point", (r, s) => XmfItems.ReadStartPoint(r, s, strings)));
        document.Blocks = List(XmfChunk.Blocks, c => XmfRecords.ReadItems<BlockDocument>(c, "block", XmfItems.ReadBlock));
        document.Spikes = List(XmfChunk.Spikes, c => XmfRecords.ReadItems<SpikeDocument>(c, "spike", XmfItems.ReadSpike));
        document.Texts = List(XmfChunk.Texts, c => XmfRecords.ReadItems<TextDocument>(c, "text", XmfItems.ReadText));
        document.Grass = List(XmfChunk.Grass, c => XmfRecords.ReadItems<GrassDocument>(c, "grass", XmfItems.ReadGrass));
        document.Flares = List(XmfChunk.Flares, c => XmfRecords.ReadItems<FlareDocument>(c, "flare", XmfItems.ReadFlare));
        document.Polygons = List(XmfChunk.Polygons, c => XmfRecords.ReadItems<PolygonDocument>(c, "polygon", (r, p) => XmfItems.ReadPolygon(r, p, strings)));
        document.ScreenReads = List(XmfChunk.ScreenReads, c => XmfRecords.ReadItems<ScreenReadDocument>(c, "screen-read region", XmfItems.ReadScreenRead));
        return document;
    }

    internal static uint ReadCount(BinaryReader reader, uint max, string what)
    {
        uint count = reader.ReadUInt32();
        if (count > max)
            throw new FormatException($"XMF {what} count is too large.");
        return count;
    }

    internal static T ReadEnum<T>(BinaryReader reader) where T : struct, Enum
    {
        byte value = reader.ReadByte();
        T result = (T)Enum.ToObject(typeof(T), value);
        if (!Enum.IsDefined(result))
            throw new FormatException($"XMF {typeof(T).Name} value {value} is out of range.");
        return result;
    }

    internal static string ClipText(string? text) =>
        text is null ? "" : text.Length > MaxTextLength ? text[..MaxTextLength] : text;
}

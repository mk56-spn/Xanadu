using System;
using System.IO;
using System.Text;
using Xanadu.Core.Items;
using Xanadu.Core.Storage;

namespace Xanadu.Serialisation.Items;

/// <summary>
/// A standalone XIF 1.0 file: one <see cref="Xif"/> tree, rig or item alike,
/// as a chunked file with a string table and the tree (see <see cref="XifBinary"/>).
/// </summary>
public static class XifFile
{
    public const ushort MajorVersion = 1;
    public const ushort MinorVersion = 0;

    private const string Format = "XIF";
    private static readonly byte[] Magic = "XIF1"u8.ToArray();

    private enum ChunkId : uint
    {
        Strings = 1,
        Tree = 2,
    }

    public static byte[] Write(Xif root)
    {
        var strings = new StringTable(Format);
        XifBinary.Index(root, strings);

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            XifBinary.Write(writer, root, strings);

        var file = new ChunkWriter(Magic, MajorVersion, MinorVersion);
        file.Add((uint)ChunkId.Strings, strings.Write());
        file.Add((uint)ChunkId.Tree, stream.ToArray());
        return file.ToArray();
    }

    /// <summary>The tree in <paramref name="data"/>; throws <see cref="FormatException"/> on a bad file.</summary>
    public static Xif Read(byte[] data)
    {
        ChunkReader file = ChunkReader.Open(data, Magic, MajorVersion, Format);
        StringTable strings = StringTable.Read(file.Get((uint)ChunkId.Strings, Format), Format);
        using BinaryReader reader = file.Get((uint)ChunkId.Tree, Format).OpenReader();
        return XifBinary.Read(reader, strings);
    }
}

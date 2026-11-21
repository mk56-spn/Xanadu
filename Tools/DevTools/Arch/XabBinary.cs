using System;
using System.IO;
using Xanadu.Core.Maths;
using Xanadu.Core.Storage;
using Xanadu.Ui.Editing;

namespace Xanadu.Tools.DevTools.Arch;

/// <summary>
/// Binary XAB reader/writer for the architecture board (<see cref="Board"/>), on the
/// shared chunked frame: the box texts in a string table, then the boxes, then the
/// lines between them.
/// </summary>
internal static class XabBinary
{
    public const ushort MajorVersion = 1, MinorVersion = 0;

    private const string Format = "XAB";

    private static readonly byte[] Magic = "XAB1"u8.ToArray();

    private enum ChunkId : uint
    {
        Strings = 1,
        Boxes = 2,
        Links = 3,
    }

    public static byte[] Write(Board board)
    {
        var strings = new StringTable(Format);
        foreach (BoardBox box in board.Boxes)
            strings.Add(box.Text);

        var file = new ChunkWriter(Magic, MajorVersion, MinorVersion);
        file.Add((uint)ChunkId.Strings, strings.Write());
        file.Add((uint)ChunkId.Boxes, Chunk(writer =>
        {
            writer.Write(board.NextId);
            writer.Write(board.Boxes.Count);
            foreach (BoardBox box in board.Boxes)
            {
                writer.Write(box.Id);
                writer.Write(box.Rect.Position.X);
                writer.Write(box.Rect.Position.Y);
                writer.Write(box.Rect.Size.X);
                writer.Write(box.Rect.Size.Y);
                strings.WriteIndex(writer, box.Text);
                writer.Write((byte)box.Category);
            }
        }));
        file.Add((uint)ChunkId.Links, Chunk(writer =>
        {
            writer.Write(board.Links.Count);
            foreach (BoardLink link in board.Links)
            {
                writer.Write(link.From);
                writer.Write(link.To);
            }
        }));
        return file.ToArray();
    }

    /// <summary>The board in <paramref name="data"/>; <see cref="InvalidDataException"/> when it is not a readable XAB.</summary>
    public static Board Read(byte[] data)
    {
        try
        {
            ChunkReader file = ChunkReader.Open(data, Magic, MajorVersion, Format);
            StringTable strings = StringTable.Read(file.Get((uint)ChunkId.Strings, Format), Format);
            var board = new Board();

            using (BinaryReader boxes = file.Get((uint)ChunkId.Boxes, Format).OpenReader())
            {
                board.NextId = boxes.ReadInt32();
                int count = boxes.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    int id = boxes.ReadInt32();
                    var rect = new Rect2(boxes.ReadSingle(), boxes.ReadSingle(), boxes.ReadSingle(), boxes.ReadSingle());
                    if (!rect.IsFinite())
                        throw new InvalidDataException("A board box has no finite size.");
                    board.Boxes.Add(new BoardBox { Id = id, Rect = rect, Text = strings.ReadIndex(boxes), Category = boxes.ReadByte() });
                    board.NextId = Math.Max(board.NextId, id + 1);
                }
            }

            using (BinaryReader links = file.Get((uint)ChunkId.Links, Format).OpenReader())
            {
                int count = links.ReadInt32();
                for (int i = 0; i < count; i++)
                    board.Link(links.ReadInt32(), links.ReadInt32());
            }

            return board;
        }
        catch (Exception exception) when (exception is FormatException or EndOfStreamException or ArgumentOutOfRangeException or IndexOutOfRangeException)
        {
            throw new InvalidDataException("The architecture board file is damaged.", exception);
        }
    }

    private static byte[] Chunk(Action<BinaryWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream))
            write(writer);
        return stream.ToArray();
    }
}

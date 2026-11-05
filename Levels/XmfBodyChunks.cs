using System;
using System.Collections.Generic;
using System.IO;
using Xanadu.Ecs;
using Xanadu.Storage;

namespace Xanadu.Levels;

/// <summary>The mine, block and spike chunks of an XMF file.</summary>
internal static class XmfBodyChunks
{
    internal static byte[] WriteMines(List<MineDocument> mines) =>
        XmfItemIO.WriteList(mines, WriteMine);

    internal static List<MineDocument> ReadMines(Chunk chunk) =>
        XmfItemIO.ReadList(chunk, XmfItemIO.MaxItems, "mine", ReadMine);

    private static void WriteMine(BinaryWriter writer, MineDocument mine)
    {
        XmfBinary.WriteGroups(writer, mine.Groups);
        writer.Write(mine.Shader);
        writer.Write(mine.TimeMs);
        writer.Write(mine.X);
        writer.Write(mine.Y);
        writer.Write(mine.FadeInMs);
        writer.Write(mine.FadeOutMs);
    }

    private static MineDocument ReadMine(BinaryReader reader) =>
        new()
        {
            Groups = XmfBinary.ReadGroups(reader),
            Shader = reader.ReadInt32(),
            TimeMs = reader.ReadInt64(),
            X = reader.ReadSingle(),
            Y = reader.ReadSingle(),
            FadeInMs = reader.ReadInt64(),
            FadeOutMs = reader.ReadInt64(),
        };

    internal static byte[] WriteBlocks(List<BlockDocument> blocks) =>
        XmfItemIO.WriteList(blocks, WriteBlock);

    internal static List<BlockDocument> ReadBlocks(Chunk chunk) =>
        XmfItemIO.ReadList(chunk, XmfItemIO.MaxItems, "block", ReadBlock);

    private static void WriteBlock(BinaryWriter writer, BlockDocument block)
    {
        XmfBinary.WriteGroups(writer, block.Groups);
        writer.Write(block.Shader);
        writer.Write(block.X);
        writer.Write(block.Y);
        writer.Write(block.Width);
        writer.Write(block.Height);
        writer.Write(block.ShapeId);
        writer.Write(block.ShapePiece);
        writer.Write((byte)block.OneWay);
        writer.Write(block.OutlineWidth);
        writer.Write(block.OutlineColor);
        writer.Write(block.FillGroups);
        writer.Write(block.OutlineGroups);
    }

    private static BlockDocument ReadBlock(BinaryReader reader)
    {
        var block = new BlockDocument
        {
            Groups = XmfBinary.ReadGroups(reader),
            Shader = reader.ReadInt32(),
            X = reader.ReadSingle(),
            Y = reader.ReadSingle(),
            Width = reader.ReadSingle(),
            Height = reader.ReadSingle(),
            ShapeId = reader.ReadInt32(),
            ShapePiece = reader.ReadInt32(),
            OneWay = reader.ReadByte(),
            OutlineWidth = reader.ReadSingle(),
            OutlineColor = reader.ReadUInt32(),
            FillGroups = reader.ReadUInt32(),
            OutlineGroups = reader.ReadUInt32(),
        };
        if (block.ShapeId < 0 || block.ShapePiece < 0)
            throw new FormatException("XMF block has a negative Form shape reference.");
        if (block.OneWay > (int)Xanadu.Ecs.BlockOneWay.Left)
            throw new FormatException("XMF block has an unknown one-way side.");
        return block;
    }

    internal static byte[] WriteSpikes(List<SpikeDocument> spikes) =>
        XmfItemIO.WriteList(spikes, WriteSpike);

    internal static List<SpikeDocument> ReadSpikes(Chunk chunk) =>
        XmfItemIO.ReadList(chunk, XmfItemIO.MaxItems, "spike", ReadSpike);

    private static void WriteSpike(BinaryWriter writer, SpikeDocument spike)
    {
        XmfBinary.WriteGroups(writer, spike.Groups);
        writer.Write(spike.Shader);
        writer.Write(spike.X);
        writer.Write(spike.Y);
        writer.Write(spike.Width);
        writer.Write(spike.Height);
        writer.Write(spike.RotationDegrees);
    }

    private static SpikeDocument ReadSpike(BinaryReader reader) =>
        new()
        {
            Groups = XmfBinary.ReadGroups(reader),
            Shader = reader.ReadInt32(),
            X = reader.ReadSingle(),
            Y = reader.ReadSingle(),
            Width = reader.ReadSingle(),
            Height = reader.ReadSingle(),
            RotationDegrees = reader.ReadSingle(),
        };
}

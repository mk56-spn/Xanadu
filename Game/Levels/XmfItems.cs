using System;
using System.IO;
using Xanadu.Ecs;
using Xanadu.Items;
using Xanadu.Settings;
using Xanadu.Storage;

namespace Xanadu.Levels;

/// <summary>
/// Each item kind's own fields, written after the shared header
/// (<see cref="XmfRecords.WriteHeader"/>). A field added to a kind goes at the
/// end of its body and is read behind <see cref="XmfRecords.More"/>.
/// </summary>
internal static class XmfItems
{
    internal static void WriteNote(BinaryWriter writer, NoteDocument note, StringTable strings)
    {
        XifBinary.Write(writer, note.Item, strings);
        XifBinary.WriteGeometry(writer, note.Item);
        writer.Write(note.TimeMs);
        writer.Write(note.X);
        writer.Write(note.Y);
        writer.Write((byte)note.Target);
        writer.Write((byte)note.Kind);
        writer.Write((byte)note.DashStrength);
        writer.Write(note.Momentum);
        writer.Write(note.Diagonal);
        writer.Write((byte)note.CostFirst);
        writer.Write((byte)note.CostSecond);
        writer.Write((byte)note.CostThird);
    }

    internal static void ReadNote(BinaryReader reader, NoteDocument note, StringTable strings)
    {
        note.Item = XifBinary.Read(reader, strings);
        XifBinary.ReadGeometry(reader, note.Item);
        note.TimeMs = reader.ReadInt64();
        note.X = reader.ReadSingle();
        note.Y = reader.ReadSingle();
        note.Target = XmfBinary.ReadEnum<KeybindAction>(reader);
        note.Kind = XmfBinary.ReadEnum<NoteKind>(reader);
        note.DashStrength = XmfBinary.ReadEnum<DashStrength>(reader);
        note.Momentum = reader.ReadSingle();
        note.Diagonal = reader.ReadBoolean();
        note.CostFirst = reader.ReadByte();
        note.CostSecond = reader.ReadByte();
        note.CostThird = reader.ReadByte();

        if (!float.IsFinite(note.Momentum) || note.Momentum < 0f)
            throw new FormatException("XMF momentum note has an invalid magnitude.");
        if (note.CostFirst > BarCosts.MaxCost || note.CostSecond > BarCosts.MaxCost || note.CostThird > BarCosts.MaxCost)
            throw new FormatException("XMF contact barrier asks more of a bar than it can.");
    }

    internal static void WriteMine(BinaryWriter writer, MineDocument mine)
    {
        writer.Write(mine.TimeMs);
        writer.Write(mine.X);
        writer.Write(mine.Y);
        writer.Write(mine.FadeInMs);
        writer.Write(mine.FadeOutMs);
    }

    internal static void ReadMine(BinaryReader reader, MineDocument mine)
    {
        mine.TimeMs = reader.ReadInt64();
        mine.X = reader.ReadSingle();
        mine.Y = reader.ReadSingle();
        mine.FadeInMs = reader.ReadInt64();
        mine.FadeOutMs = reader.ReadInt64();
    }

    internal static void WriteStartPoint(BinaryWriter writer, StartPointDocument start, StringTable strings)
    {
        strings.WriteIndex(writer, start.Name);
        writer.Write(start.TimeMs);
        writer.Write(start.X);
        writer.Write(start.Y);
        writer.Write(start.MomentumX);
        writer.Write(start.MomentumY);
        writer.Write(start.DirectionX);
        writer.Write(start.DirectionY);
    }

    internal static void ReadStartPoint(BinaryReader reader, StartPointDocument start, StringTable strings)
    {
        start.Name = strings.ReadIndex(reader);
        start.TimeMs = reader.ReadInt64();
        start.X = reader.ReadSingle();
        start.Y = reader.ReadSingle();
        start.MomentumX = reader.ReadSingle();
        start.MomentumY = reader.ReadSingle();
        start.DirectionX = reader.ReadSingle();
        start.DirectionY = reader.ReadSingle();
    }

    internal static void WriteBlock(BinaryWriter writer, BlockDocument block)
    {
        writer.Write(block.X);
        writer.Write(block.Y);
        writer.Write(block.Width);
        writer.Write(block.Height);
        writer.Write((byte)block.OneWay);
        writer.Write(block.OutlineWidth);
        writer.Write(block.OutlineColor);
        writer.Write(block.FillGroups);
        writer.Write(block.OutlineGroups);
    }

    internal static void ReadBlock(BinaryReader reader, BlockDocument block)
    {
        block.X = reader.ReadSingle();
        block.Y = reader.ReadSingle();
        block.Width = reader.ReadSingle();
        block.Height = reader.ReadSingle();
        block.OneWay = reader.ReadByte();
        block.OutlineWidth = reader.ReadSingle();
        block.OutlineColor = reader.ReadUInt32();
        block.FillGroups = reader.ReadUInt32();
        block.OutlineGroups = reader.ReadUInt32();
        if (block.OneWay > (int)BlockOneWay.Left)
            throw new FormatException("XMF block has an unknown one-way side.");
    }

    internal static void WriteSpike(BinaryWriter writer, SpikeDocument spike)
    {
        writer.Write(spike.X);
        writer.Write(spike.Y);
        writer.Write(spike.Width);
        writer.Write(spike.Height);
        writer.Write(spike.RotationDegrees);
        writer.Write(spike.OutlineWidth);
        writer.Write(spike.OutlineColor);
    }

    internal static void ReadSpike(BinaryReader reader, SpikeDocument spike)
    {
        spike.X = reader.ReadSingle();
        spike.Y = reader.ReadSingle();
        spike.Width = reader.ReadSingle();
        spike.Height = reader.ReadSingle();
        spike.RotationDegrees = reader.ReadSingle();
        spike.OutlineWidth = reader.ReadSingle();
        spike.OutlineColor = reader.ReadUInt32();
    }

    internal static void WriteFrictionBar(BinaryWriter writer, FrictionBarDocument bar)
    {
        writer.Write(bar.X);
        writer.Write(bar.Y);
        writer.Write(bar.Height);
    }

    internal static void ReadFrictionBar(BinaryReader reader, FrictionBarDocument bar)
    {
        bar.X = reader.ReadSingle();
        bar.Y = reader.ReadSingle();
        bar.Height = reader.ReadSingle();
    }

    internal static void WriteText(BinaryWriter writer, TextDocument text)
    {
        writer.Write(text.X);
        writer.Write(text.Y);
        writer.Write(text.Size);
        writer.Write(text.Rotation);
        writer.Write(XmfBinary.ClipText(text.Text));
    }

    internal static void ReadText(BinaryReader reader, TextDocument text)
    {
        text.X = reader.ReadSingle();
        text.Y = reader.ReadSingle();
        text.Size = reader.ReadSingle();
        text.Rotation = reader.ReadSingle();
        text.Text = XmfBinary.ClipText(reader.ReadString());
    }

    internal static void WriteGrass(BinaryWriter writer, GrassDocument patch)
    {
        writer.Write(patch.X);
        writer.Write(patch.Y);
        writer.Write(patch.Width);
        writer.Write(patch.Length);
        writer.Write(patch.Rotation);
        writer.Write(patch.Density);
        writer.Write(patch.Sway);
        writer.Write(patch.Color);
        writer.Write(patch.Variation);
        writer.Write(patch.Seed);
    }

    internal static void ReadGrass(BinaryReader reader, GrassDocument patch)
    {
        patch.X = reader.ReadSingle();
        patch.Y = reader.ReadSingle();
        patch.Width = reader.ReadSingle();
        patch.Length = reader.ReadSingle();
        patch.Rotation = reader.ReadSingle();
        patch.Density = reader.ReadSingle();
        patch.Sway = reader.ReadSingle();
        patch.Color = reader.ReadUInt32();
        patch.Variation = reader.ReadSingle();
        patch.Seed = reader.ReadUInt32();
    }

    internal static void WriteFlare(BinaryWriter writer, FlareDocument flare)
    {
        writer.Write(flare.X);
        writer.Write(flare.Y);
        writer.Write(flare.Width);
        writer.Write(flare.Height);
        writer.Write(flare.Rotation);
        writer.Write((byte)flare.Axes);
        writer.Write(flare.Thickness);
        writer.Write(flare.Falloff);
        writer.Write(flare.Core);
        writer.Write(flare.Halo);
        writer.Write(flare.Glow);
        writer.Write(flare.Intensity);
        writer.Write(flare.Heat);
        writer.Write(flare.Color);
    }

    internal static void ReadFlare(BinaryReader reader, FlareDocument flare)
    {
        flare.X = reader.ReadSingle();
        flare.Y = reader.ReadSingle();
        flare.Width = reader.ReadSingle();
        flare.Height = reader.ReadSingle();
        flare.Rotation = reader.ReadSingle();
        flare.Axes = reader.ReadByte();
        flare.Thickness = reader.ReadSingle();
        flare.Falloff = reader.ReadSingle();
        flare.Core = reader.ReadSingle();
        flare.Halo = reader.ReadSingle();
        flare.Glow = reader.ReadSingle();
        flare.Intensity = reader.ReadSingle();
        flare.Heat = reader.ReadSingle();
        flare.Color = reader.ReadUInt32();
    }

    internal static void WritePolygon(BinaryWriter writer, PolygonDocument polygon, StringTable strings)
    {
        writer.Write(polygon.X);
        writer.Write(polygon.Y);
        writer.Write(polygon.Rotation);
        XifBinary.Write(writer, polygon.Item, strings);
        XifBinary.WriteGeometry(writer, polygon.Item);
    }

    internal static void ReadPolygon(BinaryReader reader, PolygonDocument polygon, StringTable strings)
    {
        polygon.X = reader.ReadSingle();
        polygon.Y = reader.ReadSingle();
        polygon.Rotation = reader.ReadSingle();
        polygon.Item = XifBinary.Read(reader, strings);
        XifBinary.ReadGeometry(reader, polygon.Item);
    }

    internal static void WriteScreenRead(BinaryWriter writer, ScreenReadDocument region)
    {
        writer.Write(region.X);
        writer.Write(region.Y);
        writer.Write(region.Width);
        writer.Write(region.Height);
    }

    internal static void ReadScreenRead(BinaryReader reader, ScreenReadDocument region)
    {
        region.X = reader.ReadSingle();
        region.Y = reader.ReadSingle();
        region.Width = reader.ReadSingle();
        region.Height = reader.ReadSingle();
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xanadu.Ecs;
using Xanadu.Items;
using Xanadu.Settings;
using Xanadu.Storage;

namespace Xanadu.Levels;

/// <summary>The note and note-extra chunks of an XMF file.</summary>
internal static class XmfNoteChunks
{
    internal static byte[] WriteNotes(List<NoteDocument> notes, StringTable strings)
    {
        bool anyStrength = notes.Exists(
            note => note.DashStrength != DashStrength.Single);
        bool anyGeometry = notes.Exists(note => XifBinary.HasGeometry(note.Item));

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)notes.Count);
        foreach (NoteDocument note in notes)
        {
            XmfBinary.WriteGroups(writer, note.Groups);
            XmfItemIO.WriteItem(writer, note.Item, strings);
            writer.Write(note.TimeMs);
            writer.Write(note.X);
            writer.Write(note.Y);
            writer.Write((byte)note.Target);
            writer.Write((byte)note.Kind);
            writer.Write((byte)note.DashDirection);
            writer.Write(note.ContactRequired);
            writer.Write((byte)note.Character);
        }

        // Optional trailing strength bytes keep files written before dash
        // strength existed readable, and leave default-strength files unchanged.
        // Item geometry follows them, so strengths are written whenever it is.
        if (anyStrength || anyGeometry)
        {
            foreach (NoteDocument note in notes)
                writer.Write((byte)note.DashStrength);
        }

        if (anyGeometry)
        {
            foreach (NoteDocument note in notes)
                XifBinary.WriteGeometry(writer, note.Item);
        }

        writer.Flush();
        return stream.ToArray();
    }

    internal static List<NoteDocument> ReadNotes(Chunk chunk, StringTable strings)
    {
        using BinaryReader reader = chunk.OpenReader();
        uint count = XmfBinary.ReadCount(reader, XmfItemIO.MaxItems, "note");
        var notes = new List<NoteDocument>((int)count);
        for (uint i = 0; i < count; i++)
        {
            notes.Add(new NoteDocument
            {
                Groups = XmfBinary.ReadGroups(reader),
                Item = XmfItemIO.ReadItem(reader, strings),
                TimeMs = reader.ReadInt64(),
                X = reader.ReadSingle(),
                Y = reader.ReadSingle(),
                Target = XmfBinary.ReadEnum<KeybindAction>(reader),
                Kind = XmfBinary.ReadEnum<NoteKind>(reader),
                DashDirection = XmfBinary.ReadEnum<DashDirection>(reader),
                ContactRequired = reader.ReadInt32(),
                Character = XmfBinary.ReadEnum<CharacterKind>(reader),
            });
        }

        if (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            for (int i = 0; i < notes.Count; i++)
            {
                if (reader.BaseStream.Position >= reader.BaseStream.Length)
                    throw new FormatException("XMF note strengths are truncated.");

                notes[i].DashStrength = XmfBinary.ReadEnum<DashStrength>(reader);
            }

            if (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                foreach (NoteDocument note in notes)
                    XifBinary.ReadGeometry(reader, note.Item);
            }

            if (reader.BaseStream.Position != reader.BaseStream.Length)
                throw new FormatException("XMF notes contain trailing data.");
        }

        return notes;
    }

    /// <summary>
    /// Pair index, momentum magnitude and contact costs are newer than the note
    /// chunk. They live in an optional trailing chunk so files written before
    /// they existed still load, and readers that do not know the chunk can
    /// ignore it.
    /// </summary>
    internal static bool HasNoteExtras(List<NoteDocument> notes) => notes.Exists(
        note => note.PairIndex != PairIndex.One ||
                note.Momentum != Note.DefaultMomentum ||
                note.ContactLeft != 0 ||
                note.ContactRight != 0 ||
                note.ChannelSide != ChannelSide.Left ||
                note.HoldMs != 0 ||
                note.ShapeId != 0 ||
                note.ShapePiece != 0);

    internal static byte[] WriteNoteExtras(List<NoteDocument> notes)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)notes.Count);
        foreach (NoteDocument note in notes)
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
        writer.Flush();
        return stream.ToArray();
    }

    internal static void ReadNoteExtras(Chunk chunk, List<NoteDocument> notes)
    {
        using BinaryReader reader = chunk.OpenReader();
        uint count = XmfBinary.ReadCount(reader, XmfItemIO.MaxItems, "note extra");
        if (count != notes.Count)
            throw new FormatException("XMF note extras do not match the note count.");

        for (int i = 0; i < notes.Count; i++)
        {
            PairIndex pairIndex = XmfBinary.ReadEnum<PairIndex>(reader);
            float momentum = reader.ReadSingle();
            int contactLeft = reader.ReadInt32();
            int contactRight = reader.ReadInt32();
            ChannelSide channelSide = XmfBinary.ReadEnum<ChannelSide>(reader);
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

            notes[i].PairIndex = pairIndex;
            notes[i].Momentum = momentum;
            notes[i].ContactLeft = contactLeft;
            notes[i].ContactRight = contactRight;
            notes[i].ChannelSide = channelSide;
            notes[i].HoldMs = holdMs;
            notes[i].ShapeId = shapeId;
            notes[i].ShapePiece = shapePiece;
        }
    }

    /// <summary>Every string a Xif's chunk entry carries, so it ends up in the shared table.</summary>
}

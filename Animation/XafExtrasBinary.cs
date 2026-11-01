using System.Collections.Generic;
using System.IO;
using System.Text;
using Godot;
using Xanadu.Storage;

namespace Xanadu.Animation;

/// <summary>
/// The chunks added in minor revision 9, kept apart so the older chunks keep
/// their layout: each clip's events, and each attachment's alternate art and
/// mesh. Older files have neither.
/// </summary>
public static class XafExtrasBinary
{
    private const uint MaxEvents = 4096;
    private const uint MaxAlternates = 256;
    private const uint MaxMeshBones = 256;

    public static void Index(XafPackage package, StringTable strings)
    {
        foreach (XafClip clip in package.Clips)
        {
            foreach (XafEvent item in clip.Events)
                strings.Add(item.Name);
        }
        foreach (XafAttachment attachment in package.Attachments)
        {
            foreach (XafArt art in attachment.Alternates)
            {
                strings.Add(art.SvgPath);
                strings.Add(art.Group);
            }
            if (attachment.Mesh is { } mesh)
            {
                foreach (string bone in mesh.Bones)
                    strings.Add(bone);
            }
        }
    }

    /// <summary>One entry per clip, in the clip table's order: its event count, then each event's time and name.</summary>
    public static byte[] WriteEvents(List<XafClip> clips, StringTable strings)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)clips.Count);
        foreach (XafClip clip in clips)
        {
            writer.Write((uint)clip.Events.Count);
            foreach (XafEvent item in clip.Events)
            {
                writer.Write(item.Time);
                strings.WriteIndex(writer, item.Name);
            }
        }
        writer.Flush();
        return stream.ToArray();
    }

    public static void ReadEvents(Chunk chunk, StringTable strings, List<XafClip> clips)
    {
        using BinaryReader reader = chunk.OpenReader();
        uint count = reader.ReadUInt32();
        if (count != clips.Count)
            throw new XafFormatException("XAF event table does not match the clips.");

        foreach (XafClip clip in clips)
        {
            uint events = reader.ReadUInt32();
            if (events > MaxEvents)
                throw new XafFormatException("XAF event table is too large.");
            for (uint i = 0; i < events; i++)
                clip.Events.Add(new XafEvent { Time = reader.ReadSingle(), Name = strings.ReadIndex(reader) });
        }
    }

    /// <summary>One entry per attachment, in order: its alternate art, then its mesh if it has one.</summary>
    public static byte[] WriteAttachments(List<XafAttachment> attachments, StringTable strings)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)attachments.Count);
        foreach (XafAttachment attachment in attachments)
        {
            writer.Write((uint)attachment.Alternates.Count);
            foreach (XafArt art in attachment.Alternates)
            {
                strings.WriteIndex(writer, art.SvgPath);
                strings.WriteIndex(writer, art.Group);
            }

            writer.Write(attachment.Mesh is not null);
            if (attachment.Mesh is not { } mesh)
                continue;
            writer.Write(mesh.Detail);
            writer.Write(mesh.Blend);
            writer.Write((uint)mesh.Bones.Count);
            foreach (string bone in mesh.Bones)
                strings.WriteIndex(writer, bone);
        }
        writer.Flush();
        return stream.ToArray();
    }

    /// <summary>Added in minor revision 10: one RGBA8 colour per attachment, in order.</summary>
    public static byte[] WriteModulates(List<XafAttachment> attachments)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)attachments.Count);
        foreach (XafAttachment attachment in attachments)
            writer.Write(attachment.SelfModulate.ToRgba32());
        writer.Flush();
        return stream.ToArray();
    }

    public static void ReadModulates(Chunk chunk, List<XafAttachment> attachments)
    {
        using BinaryReader reader = chunk.OpenReader();
        if (reader.ReadUInt32() != attachments.Count)
            throw new XafFormatException("XAF attachment colours do not match the attachments.");
        foreach (XafAttachment attachment in attachments)
            attachment.SelfModulate = new Color(reader.ReadUInt32());
    }

    public static void ReadAttachments(Chunk chunk, StringTable strings, List<XafAttachment> attachments)
    {
        using BinaryReader reader = chunk.OpenReader();
        uint count = reader.ReadUInt32();
        if (count != attachments.Count)
            throw new XafFormatException("XAF attachment extras do not match the attachments.");

        foreach (XafAttachment attachment in attachments)
        {
            uint alternates = reader.ReadUInt32();
            if (alternates > MaxAlternates)
                throw new XafFormatException("XAF attachment has too much alternate art.");
            for (uint i = 0; i < alternates; i++)
                attachment.Alternates.Add(new XafArt { SvgPath = strings.ReadIndex(reader), Group = strings.ReadIndex(reader) });

            if (!reader.ReadBoolean())
                continue;
            var mesh = new XafMesh { Detail = reader.ReadInt32(), Blend = reader.ReadSingle() };
            uint bones = reader.ReadUInt32();
            if (bones > MaxMeshBones)
                throw new XafFormatException("XAF mesh has too many bones.");
            for (uint i = 0; i < bones; i++)
                mesh.Bones.Add(strings.ReadIndex(reader));
            attachment.Mesh = mesh;
        }
    }
}

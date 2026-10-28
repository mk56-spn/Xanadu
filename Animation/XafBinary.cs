using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Godot;
using Xanadu.Storage;

namespace Xanadu.Animation;

/// <summary>Binary XAF1 reader/writer with a chunk directory.</summary>
public static class XafBinary
{
    private const string Format = "XAF";

    private static readonly byte[] Magic = "XAF1"u8.ToArray();

    private enum ChunkId : uint
    {
        Strings = 1,
        Skeleton = 2,
        Attachments = 3,
        Clips = 4,
        Chains = 5,
    }

    public static void Save(string path, XafPackage package)
    {
        byte[] data = Write(package);
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllBytes(path, data);
    }

    public static XafPackage Load(string path) =>
        Read(File.ReadAllBytes(path));

    public static byte[] Write(XafPackage package) =>
        Write(package, XafPackage.MinorVersion);

    /// <summary>Writes an older minor revision; used to test reading old files.</summary>
    public static byte[] Write(XafPackage package, ushort minor)
    {
        XafValidation.ThrowIfInvalid(package);
        var strings = new StringTable(Format);
        IndexStrings(package, strings);

        var file = new ChunkWriter(Magic, XafPackage.MajorVersion, minor);
        file.Add((uint)ChunkId.Strings, strings.Write());
        file.Add((uint)ChunkId.Skeleton, WriteSkeleton(package.Skeleton, strings, minor));
        file.Add((uint)ChunkId.Attachments, WriteAttachments(package.Attachments, strings));
        file.Add((uint)ChunkId.Clips, WriteClips(package.Clips, strings));
        if (minor >= 2)
            file.Add((uint)ChunkId.Chains, WriteChains(package.Chains, strings, minor));
        return file.ToArray();
    }

    public static XafPackage Read(byte[] data)
    {
        try
        {
            return ReadPackage(data);
        }
        catch (FormatException exception)
        {
            throw new XafFormatException(exception.Message);
        }
    }

    private static XafPackage ReadPackage(byte[] data)
    {
        ChunkReader file = ChunkReader.Open(data, Magic, XafPackage.MajorVersion, Format);
        Chunk stringsChunk = file.Get((uint)ChunkId.Strings, Format);
        Chunk skeletonChunk = file.Get((uint)ChunkId.Skeleton, Format);
        Chunk attachmentsChunk = file.Get((uint)ChunkId.Attachments, Format);
        Chunk clipsChunk = file.Get((uint)ChunkId.Clips, Format);

        StringTable strings = StringTable.Read(stringsChunk, Format);
        var package = new XafPackage
        {
            Major = XafPackage.MajorVersion,
            Minor = file.Minor,
            Skeleton = ReadSkeleton(skeletonChunk, strings, file.Minor),
            Attachments = ReadAttachments(attachmentsChunk, strings),
            Clips = ReadClips(clipsChunk, strings),
        };
        if (file.Minor >= 2)
            package.Chains = ReadChains(file.Get((uint)ChunkId.Chains, Format), strings, file.Minor);
        XafValidation.ThrowIfInvalid(package);
        return package;
    }

    private static void IndexStrings(XafPackage package, StringTable table)
    {
        foreach (XafBone bone in package.Skeleton.Bones)
            table.Add(bone.Id);
        foreach (XafAttachment attachment in package.Attachments)
        {
            table.Add(attachment.SvgPath);
            table.Add(attachment.Group);
            table.Add(attachment.BoneId);
        }
        foreach (XafChain chain in package.Chains)
        {
            table.Add(chain.Id);
            table.Add(chain.BoneId);
            table.Add(chain.SvgPath);
            table.Add(chain.Group);
            table.Add(chain.CollideBoneId);
        }
        foreach (XafClip clip in package.Clips)
        {
            table.Add(clip.Id);
            foreach (XafChannel channel in clip.Channels)
                table.Add(channel.TargetId);
        }
    }

    private static byte[] WriteSkeleton(
        XafSkeleton skeleton,
        StringTable strings,
        ushort minor)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)skeleton.Bones.Count);
        foreach (XafBone bone in skeleton.Bones)
        {
            strings.WriteIndex(writer, bone.Id);
            writer.Write(bone.Parent);
            WriteTransform(writer, bone.Rest);
            writer.Write(bone.ZOrder);
            if (minor >= 1)
                writer.Write(bone.AllowTranslation);
        }
        writer.Flush();
        return stream.ToArray();
    }

    private static XafSkeleton ReadSkeleton(
        Chunk chunk,
        StringTable strings,
        ushort minor)
    {
        using BinaryReader reader = chunk.OpenReader();
        uint count = reader.ReadUInt32();
        if (count > 4096)
            throw new XafFormatException("XAF skeleton is too large.");

        var skeleton = new XafSkeleton();
        for (uint i = 0; i < count; i++)
        {
            string id = strings.ReadIndex(reader);
            int parent = reader.ReadInt32();
            if (parent < -1 || parent >= count || parent == i)
                throw new XafFormatException($"Invalid parent for bone '{id}'.");
            skeleton.Bones.Add(new XafBone
            {
                Id = id,
                Parent = parent,
                Rest = ReadTransform(reader),
                ZOrder = reader.ReadInt32(),
                AllowTranslation = minor >= 1 && reader.ReadBoolean(),
            });
        }
        return skeleton;
    }

    private static byte[] WriteAttachments(
        List<XafAttachment> attachments,
        StringTable strings)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)attachments.Count);
        foreach (XafAttachment attachment in attachments)
        {
            strings.WriteIndex(writer, attachment.SvgPath);
            strings.WriteIndex(writer, attachment.Group);
            strings.WriteIndex(writer, attachment.BoneId);
            WriteTransform(writer, attachment.Transform);
            writer.Write(attachment.ZOrder);
        }
        writer.Flush();
        return stream.ToArray();
    }

    private static List<XafAttachment> ReadAttachments(
        Chunk chunk,
        StringTable strings)
    {
        using BinaryReader reader = chunk.OpenReader();
        uint count = reader.ReadUInt32();
        if (count > 100_000)
            throw new XafFormatException("XAF attachment table is too large.");

        var attachments = new List<XafAttachment>();
        for (uint i = 0; i < count; i++)
        {
            attachments.Add(new XafAttachment
            {
                SvgPath = strings.ReadIndex(reader),
                Group = strings.ReadIndex(reader),
                BoneId = strings.ReadIndex(reader),
                Transform = ReadTransform(reader),
                ZOrder = reader.ReadInt32(),
            });
        }
        return attachments;
    }

    private static byte[] WriteChains(List<XafChain> chains, StringTable strings, ushort minor)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)chains.Count);
        foreach (XafChain chain in chains)
        {
            strings.WriteIndex(writer, chain.Id);
            strings.WriteIndex(writer, chain.BoneId);
            writer.Write(chain.Offset.X);
            writer.Write(chain.Offset.Y);
            writer.Write(chain.Segments);
            writer.Write(chain.SegmentLength);
            writer.Write(chain.Gravity);
            writer.Write(chain.Damping);
            if (minor < 3)
                continue;

            strings.WriteIndex(writer, chain.SvgPath);
            strings.WriteIndex(writer, chain.Group);
            writer.Write(chain.Width);
            writer.Write(chain.Lean);
            strings.WriteIndex(writer, chain.CollideBoneId);
            writer.Write(chain.CollideOffset.X);
            writer.Write(chain.CollideOffset.Y);
            writer.Write(chain.CollideRadius);
        }
        writer.Flush();
        return stream.ToArray();
    }

    private static List<XafChain> ReadChains(Chunk chunk, StringTable strings, ushort minor)
    {
        using BinaryReader reader = chunk.OpenReader();
        uint count = reader.ReadUInt32();
        if (count > 4096)
            throw new XafFormatException("XAF chain table is too large.");

        var chains = new List<XafChain>();
        for (uint i = 0; i < count; i++)
        {
            var chain = new XafChain
            {
                Id = strings.ReadIndex(reader),
                BoneId = strings.ReadIndex(reader),
                Offset = new Vector2(reader.ReadSingle(), reader.ReadSingle()),
                Segments = reader.ReadInt32(),
                SegmentLength = reader.ReadSingle(),
                Gravity = reader.ReadSingle(),
                Damping = reader.ReadSingle(),
            };
            if (minor >= 3)
            {
                chain.SvgPath = strings.ReadIndex(reader);
                chain.Group = strings.ReadIndex(reader);
                chain.Width = reader.ReadSingle();
                chain.Lean = reader.ReadSingle();
                chain.CollideBoneId = strings.ReadIndex(reader);
                chain.CollideOffset = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                chain.CollideRadius = reader.ReadSingle();
            }
            chains.Add(chain);
        }
        return chains;
    }

    private static byte[] WriteClips(List<XafClip> clips, StringTable strings)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)clips.Count);
        foreach (XafClip clip in clips)
        {
            strings.WriteIndex(writer, clip.Id);
            writer.Write(clip.Duration);
            writer.Write(clip.Loop);
            writer.Write(clip.LoopStart);
            writer.Write(clip.LoopEnd);
            writer.Write((uint)clip.Channels.Count);

            foreach (XafChannel channel in clip.Channels)
            {
                writer.Write((byte)channel.Target);
                strings.WriteIndex(writer, channel.TargetId);
                writer.Write((byte)channel.Property);
                writer.Write((uint)channel.Keyframes.Count);
                foreach (XafKeyframe key in channel.Keyframes)
                {
                    writer.Write(key.Time);
                    writer.Write(key.Value);
                    writer.Write((byte)key.Interpolation);
                    writer.Write(key.TangentIn);
                    writer.Write(key.TangentOut);
                }
            }
        }
        writer.Flush();
        return stream.ToArray();
    }

    private static List<XafClip> ReadClips(
        Chunk chunk,
        StringTable strings)
    {
        using BinaryReader reader = chunk.OpenReader();
        uint count = reader.ReadUInt32();
        if (count > 4096)
            throw new XafFormatException("XAF clip table is too large.");

        var clips = new List<XafClip>();
        for (uint i = 0; i < count; i++)
        {
            var clip = new XafClip
            {
                Id = strings.ReadIndex(reader),
                Duration = reader.ReadSingle(),
                Loop = reader.ReadBoolean(),
                LoopStart = reader.ReadSingle(),
                LoopEnd = reader.ReadSingle(),
            };

            uint channelCount = reader.ReadUInt32();
            if (channelCount > 100_000)
                throw new XafFormatException("XAF channel table is too large.");

            for (uint c = 0; c < channelCount; c++)
            {
                var channel = new XafChannel
                {
                    Target = (XafChannelTarget)reader.ReadByte(),
                    TargetId = strings.ReadIndex(reader),
                    Property = (XafChannelProperty)reader.ReadByte(),
                };

                uint keyCount = reader.ReadUInt32();
                if (keyCount > 1_000_000)
                    throw new XafFormatException("XAF keyframe table is too large.");

                for (uint k = 0; k < keyCount; k++)
                {
                    channel.Keyframes.Add(new XafKeyframe
                    {
                        Time = reader.ReadSingle(),
                        Value = reader.ReadSingle(),
                        Interpolation = (XafInterpolation)reader.ReadByte(),
                        TangentIn = reader.ReadSingle(),
                        TangentOut = reader.ReadSingle(),
                    });
                }
                clip.Channels.Add(channel);
            }
            clips.Add(clip);
        }
        return clips;
    }

    private static void WriteTransform(BinaryWriter writer, Transform2D transform)
    {
        writer.Write(transform.X.X);
        writer.Write(transform.X.Y);
        writer.Write(transform.Y.X);
        writer.Write(transform.Y.Y);
        writer.Write(transform.Origin.X);
        writer.Write(transform.Origin.Y);
    }

    private static Transform2D ReadTransform(BinaryReader reader) =>
        new(
            new Vector2(reader.ReadSingle(), reader.ReadSingle()),
            new Vector2(reader.ReadSingle(), reader.ReadSingle()),
            new Vector2(reader.ReadSingle(), reader.ReadSingle()));
}

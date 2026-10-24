using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Godot;

namespace Writer.Animation;

/// <summary>Binary XAF1 reader/writer with a chunk directory.</summary>
public static class XafBinary
{
    private static readonly byte[] Magic = "XAF1"u8.ToArray();

    private enum ChunkId : uint
    {
        Strings = 1,
        Skeleton = 2,
        Attachments = 3,
        Clips = 4,
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

    public static byte[] Write(XafPackage package)
    {
        XafValidation.ThrowIfInvalid(package);
        var strings = new StringTable();
        IndexStrings(package, strings);

        var chunks = new List<(ChunkId Id, byte[] Data)>
        {
            (ChunkId.Strings, WriteStrings(strings)),
            (ChunkId.Skeleton, WriteSkeleton(package.Skeleton, strings)),
            (ChunkId.Attachments, WriteAttachments(package.Attachments, strings)),
            (ChunkId.Clips, WriteClips(package.Clips, strings)),
        };

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write(package.Major);
        writer.Write(package.Minor);
        writer.Write((uint)chunks.Count);

        const int directoryEntrySize = 12;
        int directoryStart = (int)stream.Position;
        writer.Write(new byte[chunks.Count * directoryEntrySize]);

        var positions = new List<(uint Offset, uint Length)>();
        foreach ((ChunkId id, byte[] data) in chunks)
        {
            positions.Add(((uint)stream.Position, (uint)data.Length));
            writer.Write(data);
        }

        long end = stream.Position;
        stream.Position = directoryStart;
        for (int i = 0; i < chunks.Count; i++)
        {
            writer.Write((uint)chunks[i].Id);
            writer.Write(positions[i].Offset);
            writer.Write(positions[i].Length);
        }

        writer.Flush();
        return stream.ToArray();
    }

    public static XafPackage Read(byte[] data)
    {
        using var stream = new MemoryStream(data, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        Span<byte> magic = stackalloc byte[4];
        if (reader.Read(magic) != 4 || !magic.SequenceEqual(Magic))
            throw new XafFormatException("Not an XAF1 package.");

        ushort major = reader.ReadUInt16();
        ushort minor = reader.ReadUInt16();
        if (major != XafPackage.MajorVersion)
            throw new XafFormatException($"Unsupported XAF major version {major}.");

        uint chunkCount = reader.ReadUInt32();
        if (chunkCount > 64)
            throw new XafFormatException("Too many XAF chunks.");

        var directory = new Dictionary<ChunkId, (int Offset, int Length)>();
        for (uint i = 0; i < chunkCount; i++)
        {
            var id = (ChunkId)reader.ReadUInt32();
            int offset = checked((int)reader.ReadUInt32());
            int length = checked((int)reader.ReadUInt32());
            if (offset < 0 || length < 0 || offset + length > data.Length)
                throw new XafFormatException("XAF chunk outside file bounds.");
            directory[id] = (offset, length);
        }

        if (!directory.TryGetValue(ChunkId.Strings, out var stringsChunk) ||
            !directory.TryGetValue(ChunkId.Skeleton, out var skeletonChunk) ||
            !directory.TryGetValue(ChunkId.Attachments, out var attachmentsChunk) ||
            !directory.TryGetValue(ChunkId.Clips, out var clipsChunk))
        {
            throw new XafFormatException("XAF package is missing a required chunk.");
        }

        var strings = ReadStrings(data, stringsChunk);
        var package = new XafPackage
        {
            Major = major,
            Minor = minor,
            Skeleton = ReadSkeleton(data, skeletonChunk, strings),
            Attachments = ReadAttachments(data, attachmentsChunk, strings),
            Clips = ReadClips(data, clipsChunk, strings),
        };
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
        foreach (XafClip clip in package.Clips)
        {
            table.Add(clip.Id);
            foreach (XafChannel channel in clip.Channels)
                table.Add(channel.TargetId);
        }
    }

    private static byte[] WriteStrings(StringTable table)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)table.Values.Count);
        foreach (string value in table.Values)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            writer.Write((uint)bytes.Length);
            writer.Write(bytes);
        }
        writer.Flush();
        return stream.ToArray();
    }

    private static StringTable ReadStrings(
        byte[] data,
        (int Offset, int Length) chunk)
    {
        using var stream = new MemoryStream(data, chunk.Offset, chunk.Length, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8);
        uint count = reader.ReadUInt32();
        if (count > 100_000)
            throw new XafFormatException("XAF string table is too large.");

        var table = new StringTable();
        for (uint i = 0; i < count; i++)
        {
            int length = checked((int)reader.ReadUInt32());
            if (length < 0 || length > stream.Length - stream.Position)
                throw new XafFormatException("Invalid XAF string length.");
            table.Values.Add(Encoding.UTF8.GetString(reader.ReadBytes(length)));
        }
        return table;
    }

    private static byte[] WriteSkeleton(XafSkeleton skeleton, StringTable strings)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)skeleton.Bones.Count);
        foreach (XafBone bone in skeleton.Bones)
        {
            WriteStringIndex(writer, strings, bone.Id);
            writer.Write(bone.Parent);
            WriteTransform(writer, bone.Rest);
            writer.Write(bone.ZOrder);
        }
        writer.Flush();
        return stream.ToArray();
    }

    private static XafSkeleton ReadSkeleton(
        byte[] data,
        (int Offset, int Length) chunk,
        StringTable strings)
    {
        using var stream = new MemoryStream(data, chunk.Offset, chunk.Length, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8);
        uint count = reader.ReadUInt32();
        if (count > 4096)
            throw new XafFormatException("XAF skeleton is too large.");

        var skeleton = new XafSkeleton();
        for (uint i = 0; i < count; i++)
        {
            string id = ReadStringIndex(reader, strings);
            int parent = reader.ReadInt32();
            if (parent < -1 || parent >= count || parent == i)
                throw new XafFormatException($"Invalid parent for bone '{id}'.");
            skeleton.Bones.Add(new XafBone
            {
                Id = id,
                Parent = parent,
                Rest = ReadTransform(reader),
                ZOrder = reader.ReadInt32(),
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
            WriteStringIndex(writer, strings, attachment.SvgPath);
            WriteStringIndex(writer, strings, attachment.Group);
            WriteStringIndex(writer, strings, attachment.BoneId);
            WriteTransform(writer, attachment.Transform);
            writer.Write(attachment.ZOrder);
        }
        writer.Flush();
        return stream.ToArray();
    }

    private static List<XafAttachment> ReadAttachments(
        byte[] data,
        (int Offset, int Length) chunk,
        StringTable strings)
    {
        using var stream = new MemoryStream(data, chunk.Offset, chunk.Length, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8);
        uint count = reader.ReadUInt32();
        if (count > 100_000)
            throw new XafFormatException("XAF attachment table is too large.");

        var attachments = new List<XafAttachment>();
        for (uint i = 0; i < count; i++)
        {
            attachments.Add(new XafAttachment
            {
                SvgPath = ReadStringIndex(reader, strings),
                Group = ReadStringIndex(reader, strings),
                BoneId = ReadStringIndex(reader, strings),
                Transform = ReadTransform(reader),
                ZOrder = reader.ReadInt32(),
            });
        }
        return attachments;
    }

    private static byte[] WriteClips(List<XafClip> clips, StringTable strings)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)clips.Count);
        foreach (XafClip clip in clips)
        {
            WriteStringIndex(writer, strings, clip.Id);
            writer.Write(clip.Duration);
            writer.Write(clip.Loop);
            writer.Write(clip.LoopStart);
            writer.Write(clip.LoopEnd);
            writer.Write((uint)clip.Channels.Count);

            foreach (XafChannel channel in clip.Channels)
            {
                writer.Write((byte)channel.Target);
                WriteStringIndex(writer, strings, channel.TargetId);
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
        byte[] data,
        (int Offset, int Length) chunk,
        StringTable strings)
    {
        using var stream = new MemoryStream(data, chunk.Offset, chunk.Length, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8);
        uint count = reader.ReadUInt32();
        if (count > 4096)
            throw new XafFormatException("XAF clip table is too large.");

        var clips = new List<XafClip>();
        for (uint i = 0; i < count; i++)
        {
            var clip = new XafClip
            {
                Id = ReadStringIndex(reader, strings),
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
                    TargetId = ReadStringIndex(reader, strings),
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

    private static void WriteStringIndex(
        BinaryWriter writer,
        StringTable strings,
        string value) =>
        writer.Write((uint)strings.GetIndex(value));

    private static string ReadStringIndex(BinaryReader reader, StringTable strings)
    {
        uint index = reader.ReadUInt32();
        if (index >= strings.Values.Count)
            throw new XafFormatException("XAF string index out of range.");
        return strings.Values[(int)index];
    }

    private sealed class StringTable
    {
        private readonly Dictionary<string, int> _indices = new(StringComparer.Ordinal);
        public List<string> Values { get; } = new();

        public void Add(string value)
        {
            if (_indices.ContainsKey(value))
                return;
            _indices[value] = Values.Count;
            Values.Add(value);
        }

        public int GetIndex(string value)
        {
            Add(value);
            return _indices[value];
        }
    }
}

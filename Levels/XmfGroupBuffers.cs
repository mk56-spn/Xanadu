using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Godot;
using Xanadu.Rendering;

namespace Xanadu.Levels;

/// <summary>
/// The group buffer chunk of an XMF file (docs/render-architecture.md step 10):
/// which groups draw their members into one buffer, and that buffer's tint,
/// alpha, shader, keys and blend. Left out when no group is buffered.
/// </summary>
internal static class XmfGroupBuffers
{
    private const uint MaxGroups = 64;

    internal static bool HasBuffers(IChartItems items) => items.Groups.Any(group => group.Buffered);

    internal static byte[] Write(IChartItems items)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        Write(writer, items);
        writer.Flush();
        return stream.ToArray();
    }

    internal static void Write(BinaryWriter writer, IChartItems items)
    {
        List<GroupDocument> buffers = items.Groups
            .Where(group => group.Buffered)
            .OrderBy(group => group.Id)
            .ToList();
        writer.Write((uint)buffers.Count);
        foreach (GroupDocument group in buffers)
        {
            writer.Write(group.Id);
            writer.Write((byte)group.BufferBlend);
            writer.Write(new Color(group.BufferTint ?? "#ffffffff").ToRgba32());
            writer.Write(group.BufferAlpha);
            writer.Write(group.BufferShader);
            XmfShaderKeys.Write(writer, group.BufferKeys ?? new List<ShaderKeyDocument>());
        }
    }

    internal static void Read(BinaryReader reader, IChartItems items)
    {
        uint count = XmfBinary.ReadCount(reader, MaxGroups, "group buffer");
        for (uint i = 0; i < count; i++)
        {
            int id = reader.ReadInt32();
            byte blend = reader.ReadByte();
            uint tint = reader.ReadUInt32();
            float alpha = reader.ReadSingle();
            int shader = reader.ReadInt32();
            GroupDocument? group = items.Groups.FirstOrDefault(candidate => candidate.Id == id);
            if (group is null)
                continue;

            if (!Enum.IsDefined(typeof(BlendMode), (int)blend))
                throw new FormatException($"XMF group buffer blend {blend} is not a blend mode.");
            group.Buffered = true;
            group.BufferBlend = (BlendMode)blend;
            group.BufferTint = "#" + new Color(tint).ToHtml(true);
            group.BufferAlpha = alpha;
            group.BufferShader = shader;
            group.BufferKeys = new List<ShaderKeyDocument>();
            XmfShaderKeys.Read(reader, group.BufferKeys);
        }
    }
}

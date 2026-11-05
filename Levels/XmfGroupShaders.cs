using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Xanadu.Levels;

/// <summary>
/// The group shader-track chunk of an XMF file (docs/render-architecture.md
/// step 8): the shader slot and keyed instance-uniform values of every group
/// whose track has a shader. Left out when no group has one.
/// </summary>
internal static class XmfGroupShaders
{
    private const uint MaxGroups = 64;

    internal static bool HasTracks(IChartItems items) =>
        items.Groups.Any(group => group.Shader != 0 || (group.ShaderKeys?.Count ?? 0) > 0);

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
        List<GroupDocument> tracks = items.Groups
            .Where(group => group.Shader != 0 || (group.ShaderKeys?.Count ?? 0) > 0)
            .OrderBy(group => group.Id)
            .ToList();
        writer.Write((uint)tracks.Count);
        foreach (GroupDocument group in tracks)
        {
            writer.Write(group.Id);
            writer.Write(group.Shader);
            XmfShaderKeys.Write(writer, group.ShaderKeys ?? new List<ShaderKeyDocument>());
        }
    }

    internal static void Read(BinaryReader reader, IChartItems items)
    {
        uint count = XmfBinary.ReadCount(reader, MaxGroups, "group shader track");
        for (uint i = 0; i < count; i++)
        {
            int id = reader.ReadInt32();
            int shader = reader.ReadInt32();
            GroupDocument? group = items.Groups.FirstOrDefault(candidate => candidate.Id == id);
            if (group is null)
                continue;

            group.Shader = shader;
            group.ShaderKeys = new List<ShaderKeyDocument>();
            XmfShaderKeys.Read(reader, group.ShaderKeys);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Xanadu.Levels;

/// <summary>
/// Spike outlines, kept in a chunk of their own beside the spikes so maps saved
/// before spikes had outlines still load, with the default outline.
/// </summary>
public static partial class XmfBinary
{
    /// <summary>Each spike's outline, in the order the spikes chunk lists them; absent in older maps.</summary>
    internal static byte[] WriteSpikeOutlines(List<SpikeDocument> spikes)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)spikes.Count);
        foreach (SpikeDocument spike in spikes)
        {
            writer.Write(spike.OutlineWidth);
            writer.Write(spike.OutlineColor);
        }
        writer.Flush();
        return stream.ToArray();
    }

    internal static void ReadSpikeOutlines(BinaryReader reader, List<SpikeDocument> spikes)
    {
        uint count = ReadCount(reader, XmfItemIO.MaxItems, "spike outline");
        if (count != spikes.Count)
            throw new FormatException("XMF spike outlines do not match their spikes.");
        foreach (SpikeDocument spike in spikes)
        {
            spike.OutlineWidth = reader.ReadSingle();
            spike.OutlineColor = reader.ReadUInt32();
        }
    }
}

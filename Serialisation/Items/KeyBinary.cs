using System.Collections.Generic;
using System.IO;
using Xanadu.Core.Keys;
using Xanadu.Core.Storage;

namespace Xanadu.Serialisation.Items;

/// <summary>
/// The one way key channels are written, wherever they live (a rig's clips in
/// XIF, a level's groups in XMF): a count, then per channel its address (node,
/// part and field, as strings) and its keys (time, value, ease, tangents).
/// </summary>
public static class KeyBinary
{
    private const uint MaxChannels = 100_000;
    private const uint MaxKeys = 1_000_000;

    /// <summary>Puts every string the channels will write into <paramref name="strings"/>.</summary>
    public static void Index(IEnumerable<KeyChannel> channels, StringTable strings)
    {
        foreach (KeyChannel channel in channels)
        {
            strings.Add(channel.Address.Node);
            strings.Add(channel.Address.Part);
            strings.Add(channel.Address.Field);
        }
    }

    public static void WriteChannels(BinaryWriter writer, List<KeyChannel> channels, StringTable strings)
    {
        writer.Write((uint)channels.Count);
        foreach (KeyChannel channel in channels)
        {
            strings.WriteIndex(writer, channel.Address.Node);
            strings.WriteIndex(writer, channel.Address.Part);
            strings.WriteIndex(writer, channel.Address.Field);
            WriteKeys(writer, channel.Keys);
        }
    }

    public static void ReadChannels(BinaryReader reader, List<KeyChannel> into, StringTable strings)
    {
        uint count = Count(reader, MaxChannels, "key channel");
        for (uint c = 0; c < count; c++)
        {
            var channel = new KeyChannel { Address = new KeyAddress(strings.ReadIndex(reader), strings.ReadIndex(reader), strings.ReadIndex(reader)) };
            ReadKeys(reader, channel.Keys);
            into.Add(channel);
        }
    }

    public static void WriteKeys(BinaryWriter writer, List<Keyframe> keys)
    {
        writer.Write((uint)keys.Count);
        foreach (Keyframe key in keys)
        {
            writer.Write(key.Time);
            writer.Write(key.Value);
            writer.Write((byte)key.Ease);
            writer.Write(key.TangentIn);
            writer.Write(key.TangentOut);
        }
    }

    public static void ReadKeys(BinaryReader reader, List<Keyframe> into)
    {
        uint count = Count(reader, MaxKeys, "key");
        for (uint k = 0; k < count; k++)
        {
            var key = new Keyframe
            {
                Time = reader.ReadSingle(),
                Value = reader.ReadSingle(),
                Ease = (KeyEase)reader.ReadByte(),
                TangentIn = reader.ReadSingle(),
                TangentOut = reader.ReadSingle(),
            };
            if ((int)key.Ease > KeyEases.Max)
                throw new FormatException($"Key has unknown ease {(int)key.Ease}.");
            into.Add(key);
        }
    }

    private static uint Count(BinaryReader reader, uint max, string what)
    {
        uint count = reader.ReadUInt32();
        if (count > max)
            throw new FormatException($"Too many of {what}: {count}.");
        return count;
    }
}

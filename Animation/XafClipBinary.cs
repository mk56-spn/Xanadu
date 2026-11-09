using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Godot;
using Xanadu.Items;
using Xanadu.Storage;

namespace Xanadu.Animation;

/// <summary>The clip chunk of an XAF file: durations, channels, keyframes, events.</summary>
internal static class XafClipBinary
{
	internal static byte[] WriteClips(List<XafClip> clips, StringTable strings)
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

	internal static List<XafClip> ReadClips(
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

				int maxInterpolation = XafEasing.MaxInterpolation;
				for (uint k = 0; k < keyCount; k++)
				{
					var key = new XafKeyframe
					{
						Time = reader.ReadSingle(),
						Value = reader.ReadSingle(),
						Interpolation = (XafInterpolation)reader.ReadByte(),
						TangentIn = reader.ReadSingle(),
						TangentOut = reader.ReadSingle(),
					};
					if ((int)key.Interpolation > maxInterpolation)
						throw new XafFormatException($"XAF key has unknown interpolation {(int)key.Interpolation}.");
					channel.Keyframes.Add(key);
				}
				clip.Channels.Add(channel);
			}
			clips.Add(clip);
		}
		return clips;
	}
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Godot;
using Xanadu.Items;
using Xanadu.Storage;

namespace Xanadu.Animation;

/// <summary>The chain, effect and transition chunks of an XAF file.</summary>
internal static class XafFlowBinary
{
	internal static byte[] WriteChains(List<XafChain> chains, StringTable strings, ushort minor)
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
			if (minor >= 16)
			{
				writer.Write(chain.Sheet);
				writer.Write(chain.Layer.HasValue);
				writer.Write(chain.Layer ?? 0);
			}
		}
		writer.Flush();
		return stream.ToArray();
	}

	internal static List<XafChain> ReadChains(Chunk chunk, StringTable strings, ushort minor)
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
			if (minor >= 16)
			{
				chain.Sheet = reader.ReadBoolean();
				bool layered = reader.ReadBoolean();
				int layer = reader.ReadInt32();
				chain.Layer = layered ? layer : null;
			}
			chains.Add(chain);
		}
		return chains;
	}

	/// <summary>Each effect: its id, bone, resting transform and layer, then its item with the item's geometry.</summary>
	internal static byte[] WriteEffects(List<XafEffect> effects, StringTable strings)
	{
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
		writer.Write((uint)effects.Count);
		foreach (XafEffect effect in effects)
		{
			strings.WriteIndex(writer, effect.Id);
			strings.WriteIndex(writer, effect.BoneId);
			XafBinaryValues.WriteTransform(writer, effect.Transform);
			writer.Write(effect.ZOrder);
			XifBinary.Write(writer, effect.Item, strings);
			XifBinary.WriteGeometry(writer, effect.Item);
		}
		writer.Flush();
		return stream.ToArray();
	}

	internal static List<XafEffect> ReadEffects(Chunk chunk, StringTable strings)
	{
		using BinaryReader reader = chunk.OpenReader();
		uint count = reader.ReadUInt32();
		if (count > 4096)
			throw new XafFormatException("XAF effect table is too large.");

		var effects = new List<XafEffect>();
		for (uint i = 0; i < count; i++)
		{
			var effect = new XafEffect
			{
				Id = strings.ReadIndex(reader),
				BoneId = strings.ReadIndex(reader),
				Transform = XafBinaryValues.ReadTransform(reader),
				ZOrder = reader.ReadInt32(),
				Item = XifBinary.Read(reader, strings),
			};
			XifBinary.ReadGeometry(reader, effect.Item);
			effects.Add(effect);
		}
		return effects;
	}

	internal static byte[] WriteTransitions(List<XafTransition> transitions, StringTable strings)
	{
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
		writer.Write((uint)transitions.Count);
		foreach (XafTransition rule in transitions)
		{
			strings.WriteIndex(writer, rule.From);
			strings.WriteIndex(writer, rule.To);
			writer.Write(rule.Fade);
			writer.Write((byte)rule.Curve);
		}
		writer.Flush();
		return stream.ToArray();
	}

	internal static List<XafTransition> ReadTransitions(Chunk chunk, StringTable strings)
	{
		using BinaryReader reader = chunk.OpenReader();
		uint count = reader.ReadUInt32();
		if (count > 100_000)
			throw new XafFormatException("XAF transition table is too large.");

		var transitions = new List<XafTransition>();
		for (uint i = 0; i < count; i++)
		{
			transitions.Add(new XafTransition
			{
				From = strings.ReadIndex(reader),
				To = strings.ReadIndex(reader),
				Fade = reader.ReadSingle(),
				Curve = (XafFadeCurve)reader.ReadByte(),
			});
		}
		return transitions;
	}
}

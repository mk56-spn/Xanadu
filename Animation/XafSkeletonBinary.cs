using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Godot;
using Xanadu.Items;
using Xanadu.Storage;

namespace Xanadu.Animation;

/// <summary>The skeleton and attachment chunks of an XAF file.</summary>
internal static class XafSkeletonBinary
{
	internal static byte[] WriteSkeleton(
		XafSkeleton skeleton,
		StringTable strings)
	{
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
		writer.Write((uint)skeleton.Bones.Count);
		foreach (XafBone bone in skeleton.Bones)
		{
			strings.WriteIndex(writer, bone.Id);
			writer.Write(bone.Parent);
			XafBinaryValues.WriteTransform(writer, bone.Rest);
			writer.Write(bone.ZOrder);
			writer.Write(bone.AllowTranslation);
			writer.Write((sbyte)bone.Bend);
			writer.Write(bone.Limited);
			writer.Write(bone.LimitMin);
			writer.Write(bone.LimitMax);
			writer.Write(bone.Tip.X);
			writer.Write(bone.Tip.Y);
		}
		writer.Flush();
		return stream.ToArray();
	}

	internal static XafSkeleton ReadSkeleton(
		Chunk chunk,
		StringTable strings)
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
			var bone = new XafBone
			{
				Id = id,
				Parent = parent,
				Rest = XafBinaryValues.ReadTransform(reader),
				ZOrder = reader.ReadInt32(),
				AllowTranslation = reader.ReadBoolean(),
				Bend = reader.ReadSByte(),
				Limited = reader.ReadBoolean(),
				LimitMin = reader.ReadSingle(),
				LimitMax = reader.ReadSingle(),
			};
			bone.Tip = new Vector2(reader.ReadSingle(), reader.ReadSingle());
			skeleton.Bones.Add(bone);
		}
		return skeleton;
	}

	internal static byte[] WriteAttachments(
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
			XafBinaryValues.WriteTransform(writer, attachment.Transform);
			writer.Write(attachment.ZOrder);
		}
		writer.Flush();
		return stream.ToArray();
	}

	internal static List<XafAttachment> ReadAttachments(
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
				Transform = XafBinaryValues.ReadTransform(reader),
				ZOrder = reader.ReadInt32(),
			});
		}
		return attachments;
	}
}

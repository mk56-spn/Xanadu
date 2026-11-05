using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Godot;
using Xanadu.Items;
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
		Transitions = 6,
		Effects = 7,
		Cloths = 8,
		Events = 9,
		AttachmentExtras = 10,
		AttachmentModulates = 11,
		Targets = 12,
		Skins = 13,
	}

	public static void Save(string path, XafPackage package)
	{
		BinaryFile.Save(path, Write(package));
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
		file.Add((uint)ChunkId.Skeleton, XafSkeletonBinary.WriteSkeleton(package.Skeleton, strings, minor));
		file.Add((uint)ChunkId.Attachments, XafSkeletonBinary.WriteAttachments(package.Attachments, strings));
		file.Add((uint)ChunkId.Clips, XafClipBinary.WriteClips(package.Clips, strings, minor));
		if (minor >= 2)
			file.Add((uint)ChunkId.Chains, XafFlowBinary.WriteChains(package.Chains, strings, minor));
		if (minor >= 4)
			file.Add((uint)ChunkId.Transitions, XafFlowBinary.WriteTransitions(package.Transitions, strings));
		if (minor >= 5)
			file.Add((uint)ChunkId.Effects, XafFlowBinary.WriteEffects(package.Effects, strings));
		if (minor >= 6)
			file.Add((uint)ChunkId.Cloths, XafClothBinary.Write(package.Cloths, strings, minor));
		if (minor >= 9)
		{
			file.Add((uint)ChunkId.Events, XafExtrasBinary.WriteEvents(package.Clips, strings));
			file.Add((uint)ChunkId.AttachmentExtras, XafExtrasBinary.WriteAttachments(package.Attachments, strings));
		}
		if (minor >= 10)
			file.Add((uint)ChunkId.AttachmentModulates, XafExtrasBinary.WriteModulates(package.Attachments));
		if (minor >= 11)
			file.Add((uint)ChunkId.Targets, XafTargetsBinary.Write(package.Clips, strings));
		if (minor >= 15)
			file.Add((uint)ChunkId.Skins, XafSkinBinary.Write(package.Attachments, strings));
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
			Skeleton = XafSkeletonBinary.ReadSkeleton(skeletonChunk, strings, file.Minor),
			Attachments = XafSkeletonBinary.ReadAttachments(attachmentsChunk, strings),
			Clips = XafClipBinary.ReadClips(clipsChunk, strings, file.Minor),
		};
		if (file.Minor >= 2)
			package.Chains = XafFlowBinary.ReadChains(file.Get((uint)ChunkId.Chains, Format), strings, file.Minor);
		if (file.Minor >= 4)
			package.Transitions = XafFlowBinary.ReadTransitions(file.Get((uint)ChunkId.Transitions, Format), strings);
		if (file.Minor >= 5)
			package.Effects = XafFlowBinary.ReadEffects(file.Get((uint)ChunkId.Effects, Format), strings);
		if (file.Minor >= 6)
			package.Cloths = XafClothBinary.Read(file.Get((uint)ChunkId.Cloths, Format), strings, file.Minor);
		if (file.Minor >= 9)
		{
			XafExtrasBinary.ReadEvents(file.Get((uint)ChunkId.Events, Format), strings, package.Clips);
			XafExtrasBinary.ReadAttachments(file.Get((uint)ChunkId.AttachmentExtras, Format), strings, package.Attachments);
		}
		if (file.Minor >= 10)
			XafExtrasBinary.ReadModulates(file.Get((uint)ChunkId.AttachmentModulates, Format), package.Attachments);
		if (file.Minor >= 11)
			XafTargetsBinary.Read(file.Get((uint)ChunkId.Targets, Format), strings, package.Clips);
		if (file.Minor >= 15)
			XafSkinBinary.Read(file.Get((uint)ChunkId.Skins, Format), strings, package.Attachments);
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
		foreach (XafTransition rule in package.Transitions)
		{
			table.Add(rule.From);
			table.Add(rule.To);
		}
		foreach (XafEffect effect in package.Effects)
		{
			table.Add(effect.Id);
			table.Add(effect.BoneId);
			XifBinary.Index(effect.Item, table);
		}
		XafClothBinary.Index(package.Cloths, table);
		XafExtrasBinary.Index(package, table);
		XafTargetsBinary.Index(package, table);
		XafSkinBinary.Index(package, table);
	}
}

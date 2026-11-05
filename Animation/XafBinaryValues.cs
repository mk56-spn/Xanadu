using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Godot;
using Xanadu.Items;
using Xanadu.Storage;

namespace Xanadu.Animation;

/// <summary>The one transform layout every XAF chunk writes, so they all read it back the same.</summary>
internal static class XafBinaryValues
{
	internal static void WriteTransform(BinaryWriter writer, Transform2D transform)
	{
		writer.Write(transform.X.X);
		writer.Write(transform.X.Y);
		writer.Write(transform.Y.X);
		writer.Write(transform.Y.Y);
		writer.Write(transform.Origin.X);
		writer.Write(transform.Origin.Y);
	}

	internal static Transform2D ReadTransform(BinaryReader reader) =>
		new(
			new Vector2(reader.ReadSingle(), reader.ReadSingle()),
			new Vector2(reader.ReadSingle(), reader.ReadSingle()),
			new Vector2(reader.ReadSingle(), reader.ReadSingle()));
}

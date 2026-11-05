using System.Collections.Generic;
using System.IO;
using System.Text;
using Godot;
using Xanadu.Storage;

namespace Xanadu.Animation;

/// <summary>
/// The chunk added in minor revision 15: each attachment's stored skin mesh, in
/// attachment order. Per attachment, whether it has one; then its art size, its points, its
/// triangles, the bones its weights are over, and each point's weights as the
/// few bones it follows (bone slot, weight), as DragonBones and Rive keep them.
/// </summary>
public static class XafSkinBinary
{
    private const uint MaxPoints = 65536;
    private const uint MaxBones = 255;

    public static void Index(XafPackage package, StringTable strings)
    {
        foreach (XafAttachment attachment in package.Attachments)
        {
            if (attachment.Mesh?.Skin is { } skin)
            {
                foreach (string bone in skin.Bones)
                    strings.Add(bone);
            }
        }
    }

    public static byte[] Write(List<XafAttachment> attachments, StringTable strings)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)attachments.Count);
        foreach (XafAttachment attachment in attachments)
        {
            XafSkinMesh? skin = attachment.Mesh?.Skin;
            writer.Write(skin is not null);
            if (skin is null)
                continue;

            writer.Write(skin.ArtSize.X);
            writer.Write(skin.ArtSize.Y);
            writer.Write((uint)skin.Points.Length);
            foreach (Vector2 point in skin.Points)
            {
                writer.Write(point.X);
                writer.Write(point.Y);
            }
            writer.Write((uint)skin.Triangles.Length);
            foreach (int index in skin.Triangles)
                writer.Write(index);
            writer.Write((uint)skin.Bones.Count);
            foreach (string bone in skin.Bones)
                strings.WriteIndex(writer, bone);
            foreach (float[] row in skin.Weights)
            {
                byte count = 0;
                foreach (float weight in row)
                {
                    if (weight > 0f)
                        count++;
                }
                writer.Write(count);
                for (int b = 0; b < row.Length; b++)
                {
                    if (row[b] <= 0f)
                        continue;
                    writer.Write((byte)b);
                    writer.Write(row[b]);
                }
            }
        }
        writer.Flush();
        return stream.ToArray();
    }

    public static void Read(Chunk chunk, StringTable strings, List<XafAttachment> attachments)
    {
        using BinaryReader reader = chunk.OpenReader();
        if (reader.ReadUInt32() != attachments.Count)
            throw new XafFormatException("XAF skins do not match the attachments.");

        foreach (XafAttachment attachment in attachments)
        {
            if (!reader.ReadBoolean())
                continue;

            var artSize = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            uint points = reader.ReadUInt32();
            if (points > MaxPoints)
                throw new XafFormatException("XAF skin has too many points.");
            var skin = new XafSkinMesh { ArtSize = artSize, Points = new Vector2[points] };
            for (int i = 0; i < points; i++)
                skin.Points[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());

            uint indices = reader.ReadUInt32();
            if (indices > MaxPoints * 6)
                throw new XafFormatException("XAF skin has too many triangles.");
            skin.Triangles = new int[indices];
            for (int i = 0; i < indices; i++)
                skin.Triangles[i] = reader.ReadInt32();

            uint bones = reader.ReadUInt32();
            if (bones > MaxBones)
                throw new XafFormatException("XAF skin has too many bones.");
            for (int i = 0; i < bones; i++)
                skin.Bones.Add(strings.ReadIndex(reader));

            skin.Weights = new float[points][];
            for (int p = 0; p < points; p++)
            {
                var row = new float[bones];
                int count = reader.ReadByte();
                for (int k = 0; k < count; k++)
                {
                    int slot = reader.ReadByte();
                    float weight = reader.ReadSingle();
                    if (slot >= bones)
                        throw new XafFormatException("XAF skin weight names a bone it does not have.");
                    row[slot] = weight;
                }
                skin.Weights[p] = row;
            }

            // A skin without a mesh to hang on cannot be drawn; the attachment gets a default one.
            attachment.Mesh ??= new XafMesh();
            attachment.Mesh.Skin = skin;
        }
    }
}

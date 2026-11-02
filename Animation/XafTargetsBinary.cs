using System.Collections.Generic;
using System.IO;
using System.Text;
using Godot;
using Xanadu.Storage;

namespace Xanadu.Animation;

/// <summary>The chunk added in minor revision 11: each clip's limb targets, in clip order.</summary>
public static class XafTargetsBinary
{
    private const uint MaxTargets = 4096;

    public static void Index(XafPackage package, StringTable strings)
    {
        foreach (XafClip clip in package.Clips)
        {
            foreach (XafLimbTarget target in clip.Targets)
            {
                strings.Add(target.BoneId);
                strings.Add(target.AnchorId);
            }
        }
    }

    public static byte[] Write(List<XafClip> clips, StringTable strings)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)clips.Count);
        foreach (XafClip clip in clips)
        {
            writer.Write((uint)clip.Targets.Count);
            foreach (XafLimbTarget target in clip.Targets)
            {
                strings.WriteIndex(writer, target.BoneId);
                strings.WriteIndex(writer, target.AnchorId);
                writer.Write((byte)target.Shape);
                writer.Write(target.Center.X);
                writer.Write(target.Center.Y);
                writer.Write(target.RadiusX);
                writer.Write(target.RadiusY);
                writer.Write(target.Tilt);
                writer.Write(target.StartAngle);
                writer.Write(target.Sweep);
                writer.Write(target.From);
                writer.Write(target.To);
                writer.Write((byte)target.Ease);
                writer.Write(target.HoldFacing);
                writer.Write(target.Facing);
                writer.Write(target.BendPositive);
            }
        }
        writer.Flush();
        return stream.ToArray();
    }

    public static void Read(Chunk chunk, StringTable strings, List<XafClip> clips)
    {
        using BinaryReader reader = chunk.OpenReader();
        if (reader.ReadUInt32() != clips.Count)
            throw new XafFormatException("XAF target table does not match the clips.");

        foreach (XafClip clip in clips)
        {
            uint count = reader.ReadUInt32();
            if (count > MaxTargets)
                throw new XafFormatException("XAF clip has too many targets.");
            for (uint i = 0; i < count; i++)
            {
                clip.Targets.Add(new XafLimbTarget
                {
                    BoneId = strings.ReadIndex(reader),
                    AnchorId = strings.ReadIndex(reader),
                    Shape = (XafTargetShape)reader.ReadByte(),
                    Center = new Vector2(reader.ReadSingle(), reader.ReadSingle()),
                    RadiusX = reader.ReadSingle(),
                    RadiusY = reader.ReadSingle(),
                    Tilt = reader.ReadSingle(),
                    StartAngle = reader.ReadSingle(),
                    Sweep = reader.ReadSingle(),
                    From = reader.ReadSingle(),
                    To = reader.ReadSingle(),
                    Ease = (XafEasePreset)reader.ReadByte(),
                    HoldFacing = reader.ReadBoolean(),
                    Facing = reader.ReadSingle(),
                    BendPositive = reader.ReadBoolean(),
                });
            }
        }
    }
}

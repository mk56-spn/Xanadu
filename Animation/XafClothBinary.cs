using System.Collections.Generic;
using System.IO;
using System.Text;
using Godot;
using Xanadu.Storage;

namespace Xanadu.Animation;

/// <summary>
/// The XAF cloth chunk, from minor revision 6: each cloth's settings, then its
/// pins and colliders. Older files have no cloths.
/// </summary>
public static class XafClothBinary
{
    private const uint MaxCloths = 256;
    private const uint MaxPins = XafCloth.MaxColumns * XafCloth.MaxRows;
    private const uint MaxColliders = 4096;

    public static void Index(List<XafCloth> cloths, StringTable strings)
    {
        foreach (XafCloth cloth in cloths)
        {
            strings.Add(cloth.Id);
            strings.Add(cloth.BoneId);
            foreach (XafClothPin pin in cloth.Pins)
                strings.Add(pin.BoneId);
            foreach (XafClothCollider collider in cloth.Colliders)
                strings.Add(collider.BoneId);
        }
    }

    public static byte[] Write(List<XafCloth> cloths, StringTable strings)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)cloths.Count);
        foreach (XafCloth cloth in cloths)
        {
            strings.WriteIndex(writer, cloth.Id);
            strings.WriteIndex(writer, cloth.BoneId);
            writer.Write(cloth.Offset.X);
            writer.Write(cloth.Offset.Y);
            writer.Write(cloth.Columns);
            writer.Write(cloth.Rows);
            writer.Write(cloth.TopWidth);
            writer.Write(cloth.HemWidth);
            writer.Write(cloth.Length);
            writer.Write(cloth.Gravity);
            writer.Write(cloth.Lean);
            writer.Write(cloth.Damping);
            writer.Write(cloth.Stiffness);
            writer.Write(cloth.Fill.R);
            writer.Write(cloth.Fill.G);
            writer.Write(cloth.Fill.B);
            writer.Write(cloth.Fill.A);
            writer.Write(cloth.ZOrder);

            writer.Write((uint)cloth.Pins.Count);
            foreach (XafClothPin pin in cloth.Pins)
            {
                writer.Write(pin.Column);
                writer.Write(pin.Row);
                strings.WriteIndex(writer, pin.BoneId);
            }

            writer.Write((uint)cloth.Colliders.Count);
            foreach (XafClothCollider collider in cloth.Colliders)
            {
                strings.WriteIndex(writer, collider.BoneId);
                writer.Write(collider.Radius);
            }
        }
        writer.Flush();
        return stream.ToArray();
    }

    public static List<XafCloth> Read(Chunk chunk, StringTable strings)
    {
        using BinaryReader reader = chunk.OpenReader();
        uint count = reader.ReadUInt32();
        if (count > MaxCloths)
            throw new XafFormatException("XAF cloth table is too large.");

        var cloths = new List<XafCloth>();
        for (uint i = 0; i < count; i++)
        {
            var cloth = new XafCloth
            {
                Id = strings.ReadIndex(reader),
                BoneId = strings.ReadIndex(reader),
                Offset = new Vector2(reader.ReadSingle(), reader.ReadSingle()),
                Columns = reader.ReadInt32(),
                Rows = reader.ReadInt32(),
                TopWidth = reader.ReadSingle(),
                HemWidth = reader.ReadSingle(),
                Length = reader.ReadSingle(),
                Gravity = reader.ReadSingle(),
                Lean = reader.ReadSingle(),
                Damping = reader.ReadSingle(),
                Stiffness = reader.ReadSingle(),
                Fill = new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                ZOrder = reader.ReadInt32(),
            };

            uint pins = reader.ReadUInt32();
            if (pins > MaxPins)
                throw new XafFormatException("XAF cloth pin table is too large.");
            for (uint p = 0; p < pins; p++)
            {
                cloth.Pins.Add(new XafClothPin
                {
                    Column = reader.ReadInt32(),
                    Row = reader.ReadInt32(),
                    BoneId = strings.ReadIndex(reader),
                });
            }

            uint colliders = reader.ReadUInt32();
            if (colliders > MaxColliders)
                throw new XafFormatException("XAF cloth collider table is too large.");
            for (uint c = 0; c < colliders; c++)
            {
                cloth.Colliders.Add(new XafClothCollider
                {
                    BoneId = strings.ReadIndex(reader),
                    Radius = reader.ReadSingle(),
                });
            }
            cloths.Add(cloth);
        }
        return cloths;
    }
}

using System.IO;
using Xanadu.Core.Items;
using Xanadu.Core.Keys;
using Xanadu.Core.Storage;
using Xanadu.Core.Timeline;
using Xanadu.Model.Animation;

namespace Xanadu.Serialisation.Items;

/// <summary>The rig modules' binary forms: skeleton, clips, bind, variants, particles, mesh, chain and cloth.</summary>
public static partial class XifBinary
{
    private const uint MaxBones = 4096;
    private const uint MaxClips = 4096;
    private const uint MaxChannelsPerClip = 100_000;
    private const uint MaxEvents = 4096;
    private const uint MaxTargets = 4096;
    private const uint MaxSkinPoints = 65536;
    private const uint MaxSkinBones = 255;
    private const uint MaxClothPins = RigCloth.MaxColumns * RigCloth.MaxRows;

    private static void IndexRig(XifModule module, StringTable strings)
    {
        switch (module)
        {
            case RigSkeleton skeleton:
                foreach (RigBone bone in skeleton.Bones)
                    strings.Add(bone.Id);
                break;
            case XifClips clips:
                foreach (RigClip clip in clips.Clips)
                {
                    strings.Add(clip.Id);
                    KeyBinary.Index(clip.Channels, strings);
                    foreach (RigEvent item in clip.Events)
                        strings.Add(item.Name);
                    foreach (RigLimbTarget target in clip.Targets)
                    {
                        strings.Add(target.BoneId);
                        strings.Add(target.AnchorId);
                    }
                }
                foreach (RigTransition rule in clips.Transitions)
                {
                    strings.Add(rule.From);
                    strings.Add(rule.To);
                }
                break;
            case RigAttachment attachment:
                strings.Add(attachment.BoneId);
                break;
            case XifParticles particles:
                strings.Add(particles.Settings.Name);
                break;
            case RigMesh mesh:
                foreach (string bone in mesh.Bones)
                    strings.Add(bone);
                if (mesh.Skin is { } skin)
                {
                    foreach (string bone in skin.Bones)
                        strings.Add(bone);
                }
                break;
            case RigChain chain:
                strings.Add(chain.BoneId);
                strings.Add(chain.CollideBoneId);
                break;
            case RigCloth cloth:
                strings.Add(cloth.BoneId);
                foreach (RigClothPin pin in cloth.Pins)
                    strings.Add(pin.BoneId);
                foreach (RigClothCollider collider in cloth.Colliders)
                    strings.Add(collider.BoneId);
                foreach (RigClothTether tether in cloth.Tethers)
                    strings.Add(tether.BoneId);
                break;
        }
    }

    private static bool TryWriteRig(BinaryWriter writer, XifModule module, StringTable strings)
    {
        switch (module)
        {
            case RigSkeleton skeleton:
                writer.Write((byte)ModuleTag.Skeleton);
                WriteSkeleton(writer, skeleton, strings);
                return true;
            case XifClips clips:
                writer.Write((byte)ModuleTag.KeyedClips);
                WriteClips(writer, clips, strings);
                return true;
            case RigAttachment attachment:
                writer.Write((byte)ModuleTag.Attachment);
                strings.WriteIndex(writer, attachment.BoneId);
                WriteTransform(writer, attachment.Transform);
                writer.Write(attachment.ZOrder);
                WriteColor(writer, attachment.SelfModulate);
                writer.Write(attachment.Hidden);
                return true;
            case XifVariants variants:
                writer.Write((byte)ModuleTag.ShapedVariants);
                writer.Write((uint)variants.Alternates.Count);
                foreach (XifGeometry geometry in variants.Alternates)
                    WriteGeometry(writer, geometry);
                return true;
            case XifParticles particles:
                writer.Write((byte)ModuleTag.Particles);
                Xanadu.Serialisation.Particles.XpfBinary.WriteParticle(writer, particles.Settings, strings);
                return true;
            case RigMesh mesh:
                writer.Write((byte)ModuleTag.Mesh);
                WriteMesh(writer, mesh, strings);
                return true;
            case RigChain chain:
                writer.Write((byte)ModuleTag.Chain);
                WriteChain(writer, chain, strings);
                return true;
            case RigCloth cloth:
                writer.Write((byte)ModuleTag.Cloth);
                WriteCloth(writer, cloth, strings);
                return true;
            default:
                return false;
        }
    }

    private static XifModule ReadRig(BinaryReader reader, ModuleTag tag, StringTable strings)
    {
        switch (tag)
        {
            case ModuleTag.Skeleton:
                return ReadSkeleton(reader, strings);
            case ModuleTag.Clips:
                return ReadClips(reader, strings, legacy: true);
            case ModuleTag.KeyedClips:
                return ReadClips(reader, strings, legacy: false);
            case ModuleTag.Attachment:
                return new RigAttachment
                {
                    BoneId = strings.ReadIndex(reader),
                    Transform = ReadTransform(reader),
                    ZOrder = reader.ReadInt32(),
                    SelfModulate = ReadColor(reader),
                    Hidden = reader.ReadBoolean(),
                };
            case ModuleTag.Variants or ModuleTag.ShapedVariants:
            {
                var variants = new XifVariants();
                uint count = ReadCount(reader, MaxPolygons, "xif variant");
                for (uint i = 0; i < count; i++)
                    variants.Alternates.Add(ReadGeometry(reader, kinds: tag == ModuleTag.ShapedVariants));
                return variants;
            }
            case ModuleTag.Particles:
                return new XifParticles { Settings = Xanadu.Serialisation.Particles.XpfBinary.ReadParticle(reader, strings) };
            case ModuleTag.Mesh:
                return ReadMesh(reader, strings);
            case ModuleTag.Chain:
                return ReadChain(reader, strings);
            case ModuleTag.Cloth:
                return ReadCloth(reader, strings);
            default:
                throw new FormatException($"Unknown xif module {tag}.");
        }
    }

    // ------------------------------------------------------------- values

    private static void WriteTransform(BinaryWriter writer, Transform2D transform)
    {
        writer.Write(transform.X.X);
        writer.Write(transform.X.Y);
        writer.Write(transform.Y.X);
        writer.Write(transform.Y.Y);
        writer.Write(transform.Origin.X);
        writer.Write(transform.Origin.Y);
    }

    private static Transform2D ReadTransform(BinaryReader reader) =>
        new(
            new Vector2(reader.ReadSingle(), reader.ReadSingle()),
            new Vector2(reader.ReadSingle(), reader.ReadSingle()),
            new Vector2(reader.ReadSingle(), reader.ReadSingle()));

    private static void WriteColor(BinaryWriter writer, Color color)
    {
        writer.Write(color.R);
        writer.Write(color.G);
        writer.Write(color.B);
        writer.Write(color.A);
    }

    private static Color ReadColor(BinaryReader reader) =>
        new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

    private static void WriteVector(BinaryWriter writer, Vector2 value)
    {
        writer.Write(value.X);
        writer.Write(value.Y);
    }

    private static Vector2 ReadVector(BinaryReader reader) => new(reader.ReadSingle(), reader.ReadSingle());

    // ----------------------------------------------------------- skeleton

    private static void WriteSkeleton(BinaryWriter writer, RigSkeleton skeleton, StringTable strings)
    {
        writer.Write((uint)skeleton.Bones.Count);
        foreach (RigBone bone in skeleton.Bones)
        {
            strings.WriteIndex(writer, bone.Id);
            writer.Write(bone.Parent);
            WriteTransform(writer, bone.Rest);
            writer.Write(bone.ZOrder);
            writer.Write(bone.AllowTranslation);
            writer.Write((sbyte)bone.Bend);
            writer.Write(bone.Limited);
            writer.Write(bone.LimitMin);
            writer.Write(bone.LimitMax);
            WriteVector(writer, bone.Tip);
        }
    }

    private static RigSkeleton ReadSkeleton(BinaryReader reader, StringTable strings)
    {
        uint count = ReadCount(reader, MaxBones, "xif bone");
        var skeleton = new RigSkeleton();
        for (uint i = 0; i < count; i++)
        {
            string id = strings.ReadIndex(reader);
            int parent = reader.ReadInt32();
            if (parent < -1 || parent >= count || parent == i)
                throw new FormatException($"Invalid parent for bone '{id}'.");
            skeleton.Bones.Add(new RigBone
            {
                Id = id,
                Parent = parent,
                Rest = ReadTransform(reader),
                ZOrder = reader.ReadInt32(),
                AllowTranslation = reader.ReadBoolean(),
                Bend = reader.ReadSByte(),
                Limited = reader.ReadBoolean(),
                LimitMin = reader.ReadSingle(),
                LimitMax = reader.ReadSingle(),
                Tip = ReadVector(reader),
            });
        }
        return skeleton;
    }

    // -------------------------------------------------------------- clips

    private static void WriteClips(BinaryWriter writer, XifClips clips, StringTable strings)
    {
        writer.Write((uint)clips.Clips.Count);
        foreach (RigClip clip in clips.Clips)
        {
            strings.WriteIndex(writer, clip.Id);
            writer.Write(clip.Duration);
            writer.Write(clip.Loop);
            writer.Write(clip.LoopStart);
            writer.Write(clip.LoopEnd);

            KeyBinary.WriteChannels(writer, clip.Channels, strings);

            writer.Write((uint)clip.Events.Count);
            foreach (RigEvent item in clip.Events)
            {
                writer.Write(item.Time);
                strings.WriteIndex(writer, item.Name);
            }

            writer.Write((uint)clip.Targets.Count);
            foreach (RigLimbTarget target in clip.Targets)
            {
                strings.WriteIndex(writer, target.BoneId);
                strings.WriteIndex(writer, target.AnchorId);
                writer.Write((byte)target.Shape);
                WriteVector(writer, target.Center);
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

        writer.Write((uint)clips.Transitions.Count);
        foreach (RigTransition rule in clips.Transitions)
        {
            strings.WriteIndex(writer, rule.From);
            strings.WriteIndex(writer, rule.To);
            writer.Write(rule.Fade);
            writer.Write((byte)rule.Curve);
        }
    }

    /// <summary>
    /// Reads a clips module. <paramref name="legacy"/>: the first form, whose
    /// channels named a bone or part kind, an id and a rig property (as bytes),
    /// read into the same addresses; every channel since is node, part, field.
    /// </summary>
    private static XifClips ReadClips(BinaryReader reader, StringTable strings, bool legacy)
    {
        var clips = new XifClips();
        uint count = ReadCount(reader, MaxClips, "xif clip");
        for (uint i = 0; i < count; i++)
        {
            var clip = new RigClip
            {
                Id = strings.ReadIndex(reader),
                Duration = reader.ReadSingle(),
                Loop = reader.ReadBoolean(),
                LoopStart = reader.ReadSingle(),
                LoopEnd = reader.ReadSingle(),
            };

            if (legacy)
            {
                // The first form: a bone or part kind, an id and a rig property, as bytes.
                uint channels = ReadCount(reader, MaxChannelsPerClip, "xif channel");
                for (uint c = 0; c < channels; c++)
                {
                    KeyChannel channel = RigChannels.New(ReadEnum<RigChannelTarget>(reader), strings.ReadIndex(reader), ReadEnum<RigChannelProperty>(reader));
                    KeyBinary.ReadKeys(reader, channel.Keys);
                    clip.Channels.Add(channel);
                }
            }
            else
            {
                KeyBinary.ReadChannels(reader, clip.Channels, strings);
            }

            uint events = ReadCount(reader, MaxEvents, "xif event");
            for (uint e = 0; e < events; e++)
                clip.Events.Add(new RigEvent { Time = reader.ReadSingle(), Name = strings.ReadIndex(reader) });

            uint targets = ReadCount(reader, MaxTargets, "xif limb target");
            for (uint t = 0; t < targets; t++)
            {
                clip.Targets.Add(new RigLimbTarget
                {
                    BoneId = strings.ReadIndex(reader),
                    AnchorId = strings.ReadIndex(reader),
                    Shape = (RigTargetShape)reader.ReadByte(),
                    Center = ReadVector(reader),
                    RadiusX = reader.ReadSingle(),
                    RadiusY = reader.ReadSingle(),
                    Tilt = reader.ReadSingle(),
                    StartAngle = reader.ReadSingle(),
                    Sweep = reader.ReadSingle(),
                    From = reader.ReadSingle(),
                    To = reader.ReadSingle(),
                    Ease = (EasePreset)reader.ReadByte(),
                    HoldFacing = reader.ReadBoolean(),
                    Facing = reader.ReadSingle(),
                    BendPositive = reader.ReadBoolean(),
                });
            }
            clips.Clips.Add(clip);
        }

        uint transitions = ReadCount(reader, MaxClips * 4, "xif transition");
        for (uint i = 0; i < transitions; i++)
        {
            clips.Transitions.Add(new RigTransition
            {
                From = strings.ReadIndex(reader),
                To = strings.ReadIndex(reader),
                Fade = reader.ReadSingle(),
                Curve = ReadEnum<RigFadeCurve>(reader),
            });
        }
        return clips;
    }

    // --------------------------------------------------------------- mesh

    private static void WriteMesh(BinaryWriter writer, RigMesh mesh, StringTable strings)
    {
        writer.Write(mesh.Detail);
        writer.Write(mesh.Blend);
        writer.Write((uint)mesh.Bones.Count);
        foreach (string bone in mesh.Bones)
            strings.WriteIndex(writer, bone);

        RigSkinMesh? skin = mesh.Skin;
        writer.Write(skin is not null);
        if (skin is null)
            return;

        WriteVector(writer, skin.ArtSize);
        writer.Write((uint)skin.Points.Length);
        foreach (Vector2 point in skin.Points)
            WriteVector(writer, point);
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

    private static RigMesh ReadMesh(BinaryReader reader, StringTable strings)
    {
        var mesh = new RigMesh { Detail = reader.ReadInt32(), Blend = reader.ReadSingle() };
        uint meshBones = ReadCount(reader, MaxSkinBones, "xif mesh bone");
        for (uint i = 0; i < meshBones; i++)
            mesh.Bones.Add(strings.ReadIndex(reader));
        if (!reader.ReadBoolean())
            return mesh;

        Vector2 artSize = ReadVector(reader);
        uint points = ReadCount(reader, MaxSkinPoints, "xif skin point");
        var skin = new RigSkinMesh { ArtSize = artSize, Points = new Vector2[points] };
        for (int i = 0; i < points; i++)
            skin.Points[i] = ReadVector(reader);

        uint indices = ReadCount(reader, MaxSkinPoints * 6, "xif skin index");
        skin.Triangles = new int[indices];
        for (int i = 0; i < indices; i++)
            skin.Triangles[i] = reader.ReadInt32();

        uint bones = ReadCount(reader, MaxSkinBones, "xif skin bone");
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
                    throw new FormatException("Skin weight names a bone it does not have.");
                row[slot] = weight;
            }
            skin.Weights[p] = row;
        }
        mesh.Skin = skin;
        return mesh;
    }

    // -------------------------------------------------------------- chain

    private static void WriteChain(BinaryWriter writer, RigChain chain, StringTable strings)
    {
        strings.WriteIndex(writer, chain.BoneId);
        WriteVector(writer, chain.Offset);
        writer.Write(chain.Segments);
        writer.Write(chain.SegmentLength);
        writer.Write(chain.Gravity);
        writer.Write(chain.Damping);
        writer.Write(chain.Width);
        writer.Write(chain.Sheet);
        writer.Write(chain.Layer.HasValue);
        writer.Write(chain.Layer ?? 0);
        writer.Write(chain.Lean);
        strings.WriteIndex(writer, chain.CollideBoneId);
        WriteVector(writer, chain.CollideOffset);
        writer.Write(chain.CollideRadius);
    }

    private static RigChain ReadChain(BinaryReader reader, StringTable strings)
    {
        var chain = new RigChain
        {
            BoneId = strings.ReadIndex(reader),
            Offset = ReadVector(reader),
            Segments = reader.ReadInt32(),
            SegmentLength = reader.ReadSingle(),
            Gravity = reader.ReadSingle(),
            Damping = reader.ReadSingle(),
            Width = reader.ReadSingle(),
            Sheet = reader.ReadBoolean(),
        };
        bool layered = reader.ReadBoolean();
        int layer = reader.ReadInt32();
        chain.Layer = layered ? layer : null;
        chain.Lean = reader.ReadSingle();
        chain.CollideBoneId = strings.ReadIndex(reader);
        chain.CollideOffset = ReadVector(reader);
        chain.CollideRadius = reader.ReadSingle();
        return chain;
    }

    // -------------------------------------------------------------- cloth

    private static void WriteCloth(BinaryWriter writer, RigCloth cloth, StringTable strings)
    {
        strings.WriteIndex(writer, cloth.BoneId);
        WriteVector(writer, cloth.Offset);
        writer.Write(cloth.Columns);
        writer.Write(cloth.Rows);
        writer.Write(cloth.TopWidth);
        writer.Write(cloth.HemWidth);
        writer.Write(cloth.Length);
        writer.Write(cloth.Gravity);
        writer.Write(cloth.Lean);
        writer.Write(cloth.Damping);
        writer.Write(cloth.Stiffness);
        writer.Write(cloth.ZOrder);

        writer.Write((uint)cloth.Pins.Count);
        foreach (RigClothPin pin in cloth.Pins)
        {
            writer.Write(pin.Column);
            writer.Write(pin.Row);
            strings.WriteIndex(writer, pin.BoneId);
        }

        writer.Write((uint)cloth.Colliders.Count);
        foreach (RigClothCollider collider in cloth.Colliders)
        {
            strings.WriteIndex(writer, collider.BoneId);
            writer.Write(collider.Radius);
        }

        writer.Write((uint)cloth.Tethers.Count);
        foreach (RigClothTether tether in cloth.Tethers)
        {
            writer.Write(tether.Column);
            writer.Write(tether.Row);
            strings.WriteIndex(writer, tether.BoneId);
            writer.Write(tether.Radius);
        }
    }

    private static RigCloth ReadCloth(BinaryReader reader, StringTable strings)
    {
        var cloth = new RigCloth
        {
            BoneId = strings.ReadIndex(reader),
            Offset = ReadVector(reader),
            Columns = reader.ReadInt32(),
            Rows = reader.ReadInt32(),
            TopWidth = reader.ReadSingle(),
            HemWidth = reader.ReadSingle(),
            Length = reader.ReadSingle(),
            Gravity = reader.ReadSingle(),
            Lean = reader.ReadSingle(),
            Damping = reader.ReadSingle(),
            Stiffness = reader.ReadSingle(),
            ZOrder = reader.ReadInt32(),
        };

        uint pins = ReadCount(reader, MaxClothPins, "xif cloth pin");
        for (uint p = 0; p < pins; p++)
            cloth.Pins.Add(new RigClothPin { Column = reader.ReadInt32(), Row = reader.ReadInt32(), BoneId = strings.ReadIndex(reader) });

        uint colliders = ReadCount(reader, MaxBones, "xif cloth collider");
        for (uint c = 0; c < colliders; c++)
            cloth.Colliders.Add(new RigClothCollider { BoneId = strings.ReadIndex(reader), Radius = reader.ReadSingle() });

        uint tethers = ReadCount(reader, MaxClothPins, "xif cloth tether");
        for (uint t = 0; t < tethers; t++)
        {
            cloth.Tethers.Add(new RigClothTether
            {
                Column = reader.ReadInt32(), Row = reader.ReadInt32(), BoneId = strings.ReadIndex(reader), Radius = reader.ReadSingle(),
            });
        }
        return cloth;
    }
}

using Xanadu.Core.Items;
using Xanadu.Core.Keys;
using Xanadu.Core.Maths;
using Xanadu.Model.Animation;

namespace Xanadu.Logic.Animator;

/// <summary>Placeholder rig shown when no rig file exists yet: plain boxes to redraw.</summary>
public static class AnimatorDefaultPackage
{
    private const float TorsoHeight = -80f;
    private const float ShoulderHeight = -60f;
    private const float ShoulderHalfWidth = 30f;
    private const float HipHalfWidth = 14f;
    private const float LimbSegment = 40f;

    /// <summary>Standard humanoid: torso, head, two arms and two legs.</summary>
    public static RigPackage Create()
    {
        var package = new RigPackage();
        AddBone(package, "root", -1, Vector2.Zero);
        AddBone(package, "torso", 0, new Vector2(0, TorsoHeight));
        AddBone(package, "head", 1, new Vector2(0, -70));
        foreach ((string side, float sign) in new[] { ("l", -1f), ("r", 1f) })
        {
            int upperArm = AddBone(package, $"upper_arm_{side}", 1,
                new Vector2(sign * ShoulderHalfWidth, ShoulderHeight));
            int forearm = AddBone(package, $"forearm_{side}", upperArm,
                new Vector2(0, LimbSegment));
            AddBone(package, $"hand_{side}", forearm,
                new Vector2(0, LimbSegment - 4));
            int thigh = AddBone(package, $"thigh_{side}", 1,
                new Vector2(sign * HipHalfWidth, 0));
            int shin = AddBone(package, $"shin_{side}", thigh,
                new Vector2(0, LimbSegment));
            AddBone(package, $"foot_{side}", shin,
                new Vector2(0, LimbSegment));
        }

        AddAttachment(package, "torso", "torso", new Vector2(0, -35), 0, new Vector2(40, 80), new Color("#4b5563"));
        AddAttachment(package, "head", "head", new Vector2(0, -5), 2, new Vector2(36, 40), new Color("#d6b49a"));
        foreach (string side in new[] { "l", "r" })
        {
            Vector2 mid = new(0, LimbSegment / 2);
            var limb = new Vector2(12, LimbSegment + 6);
            AddAttachment(package, $"upper_arm_{side}", $"upper_arm_{side}", mid, 1, limb, new Color("#6b7280"));
            AddAttachment(package, $"forearm_{side}", $"forearm_{side}", mid, 1, limb, new Color("#6b7280"));
            AddAttachment(package, $"thigh_{side}", $"thigh_{side}", mid, -1, limb, new Color("#374151"));
            AddAttachment(package, $"shin_{side}", $"shin_{side}", mid, -1, limb, new Color("#374151"));
        }
        package.Clips.Add(Clip("idle", "torso", RigChannelProperty.Rotation, 0f, 0.06f, 2f));
        package.Clips.Add(Clip("jumping", "root", RigChannelProperty.TranslationY, 0f, -60f, 1.2f));
        package.Clips.Add(Clip("landing", "root", RigChannelProperty.TranslationY, -60f, 0f, 1.2f));
        package.Clips.Add(Clip("death", "root", RigChannelProperty.Rotation, 0f, 1.5f, 2f));
        return package;
    }

    private static int AddBone(
        RigPackage package,
        string id,
        int parent,
        Vector2 offset)
    {
        package.Skeleton.Bones.Add(new RigBone
        {
            Id = id,
            Parent = parent,
            Rest = new Transform2D(0f, offset),
        });
        return package.Skeleton.Bones.Count - 1;
    }

    /// <summary>A part on a bone: one box of <paramref name="size"/> centred on it.</summary>
    private static void AddAttachment(
        RigPackage package,
        string id,
        string bone,
        Vector2 offset,
        int zOrder,
        Vector2 size,
        Color fill)
    {
        var part = new RigAttachment { BoneId = bone, Transform = new Transform2D(0f, offset), ZOrder = zOrder };
        part.Id = id;
        Vector2 half = size / 2;
        part.Geometry.Polygons.Add(new XifPolygon
        {
            Points = { new Vector2(-half.X, -half.Y), new Vector2(half.X, -half.Y), new Vector2(half.X, half.Y), new Vector2(-half.X, half.Y) },
            Fill = fill,
        });
        package.Add(part);
    }

    private static RigClip Clip(
        string id,
        string bone,
        RigChannelProperty property,
        float start,
        float end,
        float duration)
    {
        var clip = new RigClip { Id = id, Duration = duration };
        KeyChannel channel = RigChannels.New(RigChannelTarget.Bone, bone, property);
        channel.Keys.Add(new Keyframe { Time = 0f, Value = start });
        channel.Keys.Add(new Keyframe { Time = duration, Value = end });
        clip.Channels.Add(channel);
        return clip;
    }
}

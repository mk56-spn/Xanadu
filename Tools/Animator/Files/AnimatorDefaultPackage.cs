using Godot;
using Writer.Animation;

namespace Writer.Tools.Animator.Files;

/// <summary>Placeholder rig shown when no XAF file exists yet.</summary>
public static class AnimatorDefaultPackage
{
    private const float TorsoHeight = -80f;
    private const float ShoulderHeight = -60f;
    private const float ShoulderHalfWidth = 30f;
    private const float HipHalfWidth = 14f;
    private const float LimbSegment = 40f;

    /// <summary>Standard humanoid: torso, head, two arms and two legs.</summary>
    public static XafPackage Create()
    {
        var package = new XafPackage();
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

        AddAttachment(package, "torso", "torso", "torso", new Vector2(0, -35), 0);
        AddAttachment(package, "head", "head", "head", new Vector2(0, -5), 2);
        foreach (string side in new[] { "l", "r" })
        {
            Vector2 mid = new(0, LimbSegment / 2);
            AddAttachment(package, "arm_segment", "arm_segment", $"upper_arm_{side}", mid, 1);
            AddAttachment(package, "arm_segment", "arm_segment", $"forearm_{side}", mid, 1);
            AddAttachment(package, "leg_segment", "leg_segment", $"thigh_{side}", mid, -1);
            AddAttachment(package, "leg_segment", "leg_segment", $"shin_{side}", mid, -1);
        }
        package.Clips.Add(Clip("idle", "torso", XafChannelProperty.Rotation, 0f, 0.06f, 2f));
        package.Clips.Add(Clip("jumping", "root", XafChannelProperty.TranslationY, 0f, -60f, 1.2f));
        package.Clips.Add(Clip("landing", "root", XafChannelProperty.TranslationY, -60f, 0f, 1.2f));
        package.Clips.Add(Clip("death", "root", XafChannelProperty.Rotation, 0f, 1.5f, 2f));
        return package;
    }

    private static int AddBone(
        XafPackage package,
        string id,
        int parent,
        Vector2 offset)
    {
        package.Skeleton.Bones.Add(new XafBone
        {
            Id = id,
            Parent = parent,
            Rest = new Transform2D(0f, offset),
        });
        return package.Skeleton.Bones.Count - 1;
    }

    private static void AddAttachment(
        XafPackage package,
        string file,
        string group,
        string bone,
        Vector2 offset,
        int zOrder) =>
        package.Attachments.Add(new XafAttachment
        {
            SvgPath = $"res://Assets/SVG/{file}.svg",
            Group = group,
            BoneId = bone,
            Transform = new Transform2D(0f, offset),
            ZOrder = zOrder,
        });

    private static XafClip Clip(
        string id,
        string bone,
        XafChannelProperty property,
        float start,
        float end,
        float duration)
    {
        var clip = new XafClip { Id = id, Duration = duration };
        var channel = new XafChannel
        {
            Target = XafChannelTarget.Bone,
            TargetId = bone,
            Property = property,
        };
        channel.Keyframes.Add(new XafKeyframe { Time = 0f, Value = start });
        channel.Keyframes.Add(new XafKeyframe { Time = duration, Value = end });
        clip.Channels.Add(channel);
        return clip;
    }
}

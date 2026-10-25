using Godot;
using Writer.Animation;

namespace Writer.Tools.Animator.Files;

/// <summary>Placeholder rig shown when no XAF file exists yet.</summary>
public static class AnimatorDefaultPackage
{
    private const float TorsoHeight = -80f;

    public static XafPackage Create()
    {
        var package = new XafPackage();
        AddBone(package, "root", -1, Vector2.Zero);
        AddBone(package, "torso", 0, new Vector2(0, TorsoHeight));
        AddBone(package, "head", 1, new Vector2(0, -70));
        AddBone(package, "arm", 1, new Vector2(55, -40));
        AddAttachment(package, "body", "torso", new Vector2(0, -20));
        AddAttachment(package, "head", "head", new Vector2(0, -5));
        AddAttachment(package, "arm", "arm", new Vector2(55, 0));
        package.Clips.Add(Clip("idle", "torso", XafChannelProperty.Rotation, 0f, 0.06f, 2f));
        package.Clips.Add(Clip("jumping", "root", XafChannelProperty.TranslationY, 0f, -60f, 1.2f));
        package.Clips.Add(Clip("landing", "root", XafChannelProperty.TranslationY, -60f, 0f, 1.2f));
        package.Clips.Add(Clip("death", "root", XafChannelProperty.Rotation, 0f, 1.5f, 2f));
        return package;
    }

    private static void AddBone(
        XafPackage package,
        string id,
        int parent,
        Vector2 offset) =>
        package.Skeleton.Bones.Add(new XafBone
        {
            Id = id,
            Parent = parent,
            Rest = new Transform2D(0f, offset),
        });

    private static void AddAttachment(
        XafPackage package,
        string group,
        string bone,
        Vector2 offset) =>
        package.Attachments.Add(new XafAttachment
        {
            SvgPath = $"res://Assets/SVG/{group}.svg",
            Group = group,
            BoneId = bone,
            Transform = new Transform2D(0f, offset),
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

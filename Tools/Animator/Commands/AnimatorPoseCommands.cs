using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Writer.Animation;

namespace Writer.Tools.Animator.Commands;

/// <summary>Whole-pose edits: key every bone, copy and paste a pose, and mirror it left to right.</summary>
public static class AnimatorPoseCommands
{
    /// <summary>Bone name endings that pair a left bone with its right twin.</summary>
    private static readonly (string Left, string Right)[] Sides =
    {
        ("_l", "_r"), ("_L", "_R"), (".l", ".r"), (".L", ".R"), ("-l", "-r"),
    };

    /// <summary>Keys every bone's current pose at the playhead.</summary>
    public static void KeyPose(Entity session) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            if (!TryCurrentClip(session, out XafClip clip))
                return;

            ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
            float time = document.Runtime.CurrentTime;
            XafPose pose = document.Runtime.Pose;
            for (int i = 0; i < document.Package.Skeleton.Bones.Count; i++)
                XafAuthoring.KeyBoneTransform(document.Package, clip, i, time, pose.Bones[i]);
            AnimatorSession.SetStatus(session, $"KEYED POSE AT {time:0.00}s");
        });

    /// <summary>Copies every bone's current pose, by bone id, so it can be pasted at another time or clip.</summary>
    public static void CopyPose(Entity session)
    {
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        var pose = new Dictionary<string, XafTransform>();
        for (int i = 0; i < document.Package.Skeleton.Bones.Count; i++)
            pose[document.Package.Skeleton.Bones[i].Id] = document.Runtime.Pose.Bones[i];

        session.GetComponent<AnimatorClipboard>().Pose = pose;
        AnimatorSession.SetStatus(session, $"COPIED POSE AT {document.Runtime.CurrentTime:0.00}s");
    }

    /// <summary>Poses the bones the copied pose names; keyed straight away with auto key.</summary>
    public static void PastePose(Entity session)
    {
        Dictionary<string, XafTransform>? copied = session.GetComponent<AnimatorClipboard>().Pose;
        if (copied is null)
        {
            AnimatorSession.SetStatus(session, "COPY A POSE FIRST");
            return;
        }

        if (!CanPose(session))
            return;

        ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
        document.Runtime.SetPaused(true);
        for (int i = 0; i < document.Package.Skeleton.Bones.Count; i++)
        {
            if (copied.TryGetValue(document.Package.Skeleton.Bones[i].Id, out XafTransform transform))
                document.Runtime.Pose.Bones[i] = transform;
        }

        Finish(session, "PASTED POSE");
    }

    /// <summary>
    /// Mirrors the pose left to right: paired bones (<c>arm_l</c>/<c>arm_r</c>)
    /// swap poses and every turn and sideways shift is reversed.
    /// </summary>
    public static void MirrorPose(Entity session)
    {
        if (!CanPose(session))
            return;

        ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
        document.Runtime.SetPaused(true);
        List<XafBone> bones = document.Package.Skeleton.Bones;
        XafTransform[] before = (XafTransform[])document.Runtime.Pose.Bones.Clone();
        for (int i = 0; i < bones.Count; i++)
        {
            int twin = XafRig.FindBone(document.Package, MirrorId(bones[i].Id));
            XafTransform source = before[twin >= 0 ? twin : i];
            source.Rotation = -source.Rotation;
            // Fixed-length bones keep their own offset; only movable ones are shifted.
            source.Translation = bones[i].CanTranslate
                ? new Vector2(-source.Translation.X, source.Translation.Y)
                : before[i].Translation;
            document.Runtime.Pose.Bones[i] = source;
        }

        Finish(session, "MIRRORED POSE");
    }

    /// <summary>The id of a bone's other-side twin, or the id itself for a centre bone.</summary>
    public static string MirrorId(string id)
    {
        foreach ((string left, string right) in Sides)
        {
            if (id.EndsWith(left, System.StringComparison.Ordinal))
                return id[..^left.Length] + right;
            if (id.EndsWith(right, System.StringComparison.Ordinal))
                return id[..^right.Length] + left;
        }
        return id;
    }

    private static bool CanPose(Entity session)
    {
        if (!session.GetComponent<AnimatorModes>().Rest)
            return true;

        AnimatorSession.SetStatus(session, "LEAVE REST POSE TO EDIT THE POSE");
        return false;
    }

    private static void Finish(Entity session, string status)
    {
        if (session.GetComponent<AnimatorModes>().AutoKey)
        {
            KeyPose(session);
            status += " AND KEYED";
        }
        AnimatorSession.SetStatus(session, status);
    }

    private static bool TryCurrentClip(Entity session, out XafClip clip)
    {
        clip = null!;
        if (session.GetComponent<AnimatorModes>().Rest)
        {
            AnimatorSession.SetStatus(session, "LEAVE REST POSE TO KEY");
            return false;
        }

        XafClip? current = AnimatorSession.CurrentClip(session.GetComponent<AnimatorDocument>());
        if (current is null)
            return false;
        clip = current;
        return true;
    }
}

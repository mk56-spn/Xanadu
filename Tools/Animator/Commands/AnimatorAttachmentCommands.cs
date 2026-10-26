using Friflo.Engine.ECS;
using Godot;
using Writer.Animation;

namespace Writer.Tools.Animator.Commands;

/// <summary>Attachments: the rigid SVG groups that hang off a bone.</summary>
public static class AnimatorAttachmentCommands
{
    /// <summary>
    /// Adds an SVG file's group to a bone as a rigid attachment at the bone's
    /// origin, selects it and rebuilds the rig, as one undo step. The file and
    /// group come from <see cref="AnimatorSvgChoice"/>, so the group exists.
    /// </summary>
    public static void Add(Entity session, string svgPath, string group, int bone) =>
        AnimatorHistoryCommands.Edit(session, () => AddCore(session, svgPath, group, bone));

    private static void AddCore(Entity session, string svgPath, string group, int bone)
    {
        ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
        XafPackage package = document.Package;
        if (bone < 0 || bone >= package.Skeleton.Bones.Count)
        {
            AnimatorSession.SetStatus(session, "SELECT A BONE FIRST");
            return;
        }

        string boneId = package.Skeleton.Bones[bone].Id;
        package.Attachments.Add(new XafAttachment
        {
            SvgPath = svgPath,
            Group = group,
            BoneId = boneId,
            Transform = Transform2D.Identity,
        });
        AnimatorSession.RigChanged(session);

        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        selection.Bone = -1;
        selection.Attachment = package.Attachments.Count - 1;
        selection.HasKey = false;
        AnimatorSession.SetStatus(session, $"ADDED {group} TO {boneId}");
    }
}

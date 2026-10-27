using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Animation;

namespace Xanadu.Tools.Animator.Commands;

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
        selection.SelectOnlyAttachment(package.Attachments.Count - 1);
        selection.HasKey = false;
        AnimatorSession.SetStatus(session, $"ADDED {group} TO {boneId}");
    }

    /// <summary>
    /// The attachments the layer commands act on: the selected SVG, or every
    /// SVG on the selected bones, so a whole limb can be moved at once.
    /// </summary>
    public static List<int> LayerTargets(Entity session)
    {
        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        AnimatorSelection selection = session.GetComponent<AnimatorSelection>();
        var targets = new List<int>();
        if (selection.Attachment >= 0 && selection.Attachment < package.Attachments.Count)
        {
            targets.Add(selection.Attachment);
            return targets;
        }

        var ids = new HashSet<string>();
        foreach (int bone in selection.Bones())
        {
            if (bone < package.Skeleton.Bones.Count)
                ids.Add(package.Skeleton.Bones[bone].Id);
        }

        for (int i = 0; i < package.Attachments.Count; i++)
        {
            if (ids.Contains(package.Attachments[i].BoneId))
                targets.Add(i);
        }
        return targets;
    }

    /// <summary>The draw layer the targets share, or false when there are none or they differ.</summary>
    public static bool TryGetLayer(Entity session, out int layer)
    {
        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        List<int> targets = LayerTargets(session);
        int first = targets.Count > 0 ? package.Attachments[targets[0]].ZOrder : 0;
        layer = first;
        return targets.Count > 0 && targets.TrueForAll(i => package.Attachments[i].ZOrder == first);
    }

    /// <summary>
    /// Sets the draw layer of the selected SVG, or of every SVG on the selected
    /// bones. Higher layers draw in front; equal ones keep their list order.
    /// </summary>
    public static void SetLayer(Entity session, int layer) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            List<int> targets = LayerTargets(session);
            if (targets.Count == 0)
            {
                AnimatorSession.SetStatus(session, "SELECT A BONE OR SVG WITH ART FIRST");
                return;
            }

            XafPackage package = session.GetComponent<AnimatorDocument>().Package;
            int clamped = System.Math.Clamp(layer, XafAttachment.MinLayer, XafAttachment.MaxLayer);
            foreach (int index in targets)
                package.Attachments[index].ZOrder = clamped;

            // The art is drawn from its own canvas items, which take the new order on a reload.
            AnimatorSession.RigChanged(session);
            AnimatorSession.SetStatus(
                session,
                targets.Count == 1 ? $"LAYER {clamped}" : $"LAYER {clamped} ON {targets.Count} SVGS");
        });
}

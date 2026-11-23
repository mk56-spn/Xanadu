using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core.Items;
using Xanadu.Core.Layering;
using Xanadu.Model.Animation;
using Xanadu.Model.Animator;
using Xanadu.Model.Animator.Intents;

namespace Xanadu.Logic.Animator.Commands;

/// <summary>Parts: nodes bound to a bone, drawn from their polygons.</summary>
public static class AnimatorAttachmentCommands
{
    /// <summary>Hears this class's intents on <paramref name="session"/>.</summary>
    public static void Listen(Entity session)
    {
        session.On<AddPart>((s, _) => Add(s, AnimatorSessionCommands.SelectedBone(s)));
    }

    /// <summary>
    /// Adds a part to a bone at the bone's origin, with a starting square to
    /// draw from in the polygon editor, selects it and rebuilds the rig, as one
    /// undo step. It is named after the bone.
    /// </summary>
    public static void Add(Entity session, int bone) =>
        AnimatorHistoryCommands.Edit(session, () => AddCore(session, bone));

    private static void AddCore(Entity session, int bone)
    {
        ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
        RigPackage package = document.Package;
        if (bone < 0 || bone >= package.Skeleton.Bones.Count)
        {
            AnimatorSession.SetStatus(session, "SELECT A BONE FIRST");
            return;
        }

        string boneId = package.Skeleton.Bones[bone].Id;
        var part = new RigAttachment { BoneId = boneId, Transform = Transform2D.Identity };
        part.Id = package.UniqueId(boneId);
        XifEditing.AddPolygon(part.Node);
        package.Add(part);
        AnimatorSession.RigChanged(session);

        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        selection.SelectOnlyAttachment(package.Attachments.IndexOf(part));
        selection.HasKey = false;
        AnimatorSession.SetStatus(session, $"ADDED {part.Id} TO {boneId} · DRAW IT IN THE ART EDITOR");
    }

    /// <summary>
    /// The attachments the layer commands act on: the selected part, or every
    /// part on the selected bones, so a whole limb can be moved at once.
    /// </summary>
    public static List<int> LayerTargets(Entity session)
    {
        RigPackage package = session.GetComponent<AnimatorDocument>().Package;
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
        RigPackage package = session.GetComponent<AnimatorDocument>().Package;
        List<int> targets = LayerTargets(session);
        int first = targets.Count > 0 ? package.Attachments[targets[0]].ZOrder : 0;
        layer = first;
        return targets.Count > 0 && targets.TrueForAll(i => package.Attachments[i].ZOrder == first);
    }

    /// <summary>
    /// Sets the draw layer of the selected part, or of every part on the selected
    /// bones. Higher layers draw in front; equal ones keep their list order.
    /// </summary>
    public static void SetLayer(Entity session, int layer) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            List<int> targets = LayerTargets(session);
            if (targets.Count == 0)
            {
                AnimatorSession.SetStatus(session, "SELECT A BONE OR PART WITH ART FIRST");
                return;
            }

            RigPackage package = session.GetComponent<AnimatorDocument>().Package;
            int clamped = DrawOrder.ClampLayer(layer);
            foreach (int index in targets)
                package.Attachments[index].ZOrder = clamped;

            // The art is drawn from its own canvas items, which take the new order on a reload.
            AnimatorSession.RigChanged(session);
            AnimatorSession.SetStatus(
                session,
                targets.Count == 1 ? $"LAYER {clamped}" : $"LAYER {clamped} ON {targets.Count} PARTS");
        });
}

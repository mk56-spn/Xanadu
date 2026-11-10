using Friflo.Engine.ECS;
using Xanadu.Animation;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Systems.Input;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>Highlights the joint, label or attachment a click in the preview would pick.</summary>
internal static class AnimatorPreviewPick
{
    public static void Hover(Entity session, Rect2 area, PointerInput input, bool okay)
    {
        ref AnimatorHover hover = ref session.GetComponent<AnimatorHover>();
        if (!okay || !area.HasPoint(input.Position))
        {
            hover = AnimatorHover.None;
            return;
        }

        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        AnimatorModes modes = session.GetComponent<AnimatorModes>();
        AnimatorPreviewView view = session.GetComponent<AnimatorPreviewView>();
        Transform2D placement = AnimatorGeometry.PreviewPlacement(area, view);
        XafPose pose = AnimatorGeometry.PreviewPose(document, modes);
        Transform2D[] world = XafRig.WorldBones(document.Package, pose);

        if (modes.Svg)
        {
            Transform2D[] attachments = XafRig.WorldAttachments(document.Package, pose, world);
            hover = new AnimatorHover
            {
                Bone = -1,
                Attachment = AnimatorGeometry.PickNearest(
                    attachments, placement, input.Position, AnimatorGeometry.AttachmentHitRadius),
            };
            return;
        }

        // A click waiting to pick a bone ignores the rotation handles, so hovering does too.
        int tip = AnimatorTipHandles.Shown(modes) && !AwaitingPick(session)
            ? AnimatorTipHandles.Pick(document.Package, world, placement, input.Position)
            : -1;
        hover = new AnimatorHover
        {
            Bone = tip >= 0 ? tip : AnimatorGeometry.PickBoneOrLabel(world, placement, input.Position),
            Attachment = -1,
        };
    }

    /// <summary>Whether the next click answers a reparent, chain, mesh or cloth pick.</summary>
    private static bool AwaitingPick(Entity session) =>
        session.GetComponent<AnimatorBoneMenu>().Reparenting >= 0 ||
        session.GetComponent<AnimatorChains>().Picking != AnimatorChainPick.None ||
        session.GetComponent<AnimatorMeshPick>().Active ||
        session.GetComponent<AnimatorWeightPaint>().Active ||
        session.GetComponent<AnimatorCloths>().Picking != AnimatorClothPick.None;
}

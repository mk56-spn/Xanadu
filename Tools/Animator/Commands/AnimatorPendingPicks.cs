using Friflo.Engine.ECS;

namespace Xanadu.Tools.Animator.Commands;

/// <summary>
/// Clears the picks and modes that a click armed, before the next action runs:
/// a chain or cloth button waiting for a bone, a mesh pick, a weight brush.
/// </summary>
public static class AnimatorPendingPicks
{
    public static void Dismiss(Entity session, ToolActionKind kind)
    {
        // Any other action withdraws a pending "discard unsaved changes?" question.
        if (kind is not (ToolActionKind.Load or ToolActionKind.NewSkeleton))
            session.GetComponent<AnimatorFile>().DiscardArmed = false;

        // And a chain button waiting for a bone click: pressing anything else drops it.
        if (kind is not (ToolActionKind.PickChainBone or ToolActionKind.PickColliderBone))
            session.GetComponent<AnimatorChains>().Picking = AnimatorChainPick.None;
        if (kind is not (ToolActionKind.PickClothBone or ToolActionKind.PinClothPoints or
            ToolActionKind.PickClothColliders))
        {
            session.GetComponent<AnimatorCloths>().Picking = AnimatorClothPick.None;
        }

        if (kind != ToolActionKind.PickMeshBones)
            session.GetComponent<AnimatorMeshPick>().Active = false;

        // Painting weights stays on through its own buttons, undo and choosing a bone.
        if (kind is not (ToolActionKind.PaintWeights or ToolActionKind.AutoWeights or
            ToolActionKind.BrushSmaller or ToolActionKind.BrushLarger or ToolActionKind.BrushWeaker or
            ToolActionKind.BrushStronger or ToolActionKind.Undo or ToolActionKind.Redo or ToolActionKind.SelectBone))
        {
            session.GetComponent<AnimatorWeightPaint>().Active = false;
        }
    }
}

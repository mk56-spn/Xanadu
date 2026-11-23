using Friflo.Engine.ECS;
using Xanadu.Model.Animator;
using Xanadu.Model.Animator.Intents;

namespace Xanadu.Logic.Animator.Commands;

/// <summary>
/// Clears the picks and modes that a click armed, before the next intent runs:
/// a chain or cloth button waiting for a bone, a mesh pick, a weight brush.
/// </summary>
public static class AnimatorPendingPicks
{
    public static void Dismiss<T>(Entity session, in T intent)
        where T : struct
    {
        // Any other intent withdraws a pending "discard unsaved changes?" question.
        if (intent is not (Load or NewSkeleton))
            session.GetComponent<AnimatorFile>().DiscardArmed = false;

        // And a chain or cloth button waiting for a bone click: pressing anything else drops it.
        if (intent is not PickChainBone)
            session.GetComponent<AnimatorChains>().Picking = AnimatorChainPick.None;
        if (intent is not PickCloth)
            session.GetComponent<AnimatorCloths>().Picking = AnimatorClothPick.None;

        if (intent is not PickMeshBones)
            session.GetComponent<AnimatorMeshPick>().Active = false;

        // Painting weights stays on through its own buttons, undo and choosing a bone.
        if (intent is not (PaintWeights or AutoWeights or StepBrushRadius or StepBrushStrength
            or Undo or Redo or SelectBone))
        {
            session.GetComponent<AnimatorWeightPaint>().Active = false;
        }
    }
}

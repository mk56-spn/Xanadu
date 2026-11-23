using Friflo.Engine.ECS;
using Xanadu.Model.Animation;
using Xanadu.Tools.Animator.Ecs;
using Xanadu.Tools.Animator.Ui;

namespace Xanadu.Tools.Animator.Commands;

/// <summary>
/// Where a chain hangs and what it looks like: choosing its art, and the bones
/// its first point and its collider follow. The numbers and dragging are in
/// <see cref="AnimatorChainCommands"/> and <see cref="AnimatorChainHandles"/>.
/// </summary>
public static class AnimatorChainPlacementCommands
{
    private static bool TryChain(Entity session, out RigChain chain, out int index)
    {
        RigPackage package = session.GetComponent<AnimatorDocument>().Package;
        index = session.GetComponent<AnimatorSelection>().Chain;
        chain = null!;
        if (index < 0 || index >= package.Chains.Count)
        {
            AnimatorSession.SetStatus(session, "SELECT A CHAIN FIRST");
            return false;
        }

        chain = package.Chains[index];
        return true;
    }

    /// <summary>The next bone clicked, in the preview or the tree, becomes the one named by <paramref name="pick"/>.</summary>
    public static void BeginPick(Entity session, AnimatorChainPick pick)
    {
        if (!TryChain(session, out RigChain chain, out _))
            return;

        session.GetComponent<AnimatorChains>().Picking = pick;
        AnimatorSession.SetStatus(
            session,
            pick == AnimatorChainPick.Anchor
                ? $"CLICK THE BONE {chain.Id} HANGS FROM"
                : $"CLICK THE BONE {chain.Id}'S COLLIDER FOLLOWS");
    }

    public static void CancelPick(Entity session)
    {
        session.GetComponent<AnimatorChains>().Picking = AnimatorChainPick.None;
        AnimatorSession.SetStatus(session, "CANCELLED");
    }

    /// <summary>Gives the bone to a pending pick, keeping the chain or collider where it is on screen.</summary>
    public static void PickBone(Entity session, int bone)
    {
        ref AnimatorChains chains = ref session.GetComponent<AnimatorChains>();
        AnimatorChainPick pick = chains.Picking;
        chains.Picking = AnimatorChainPick.None;
        if (pick == AnimatorChainPick.None)
            return;

        AnimatorHistoryCommands.Edit(session, () =>
        {
            RigPackage package = session.GetComponent<AnimatorDocument>().Package;
            int index = session.GetComponent<AnimatorSelection>().Chain;
            bool done = pick == AnimatorChainPick.Anchor
                ? RigChainAuthoring.SetBone(package, index, bone)
                : RigChainAuthoring.SetColliderBone(package, index, bone);
            if (!done)
            {
                AnimatorSession.SetStatus(session, "CANCELLED");
                return;
            }

            string boneId = package.Skeleton.Bones[bone].Id;
            string chain = package.Chains[index].Id;
            AnimatorSession.SetStatus(
                session,
                pick == AnimatorChainPick.Anchor ? $"{chain} HANGS FROM {boneId}" : $"{chain} COLLIDES WITH {boneId}");
        });
    }
}

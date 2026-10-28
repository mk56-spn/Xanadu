using Friflo.Engine.ECS;
using Xanadu.Animation;

namespace Xanadu.Tools.Animator.Commands;

/// <summary>
/// Where a chain hangs and what it looks like: choosing its art, and the bones
/// its first point and its collider follow. The numbers and dragging are in
/// <see cref="AnimatorChainCommands"/> and <see cref="AnimatorChainHandles"/>.
/// </summary>
public static class AnimatorChainPlacementCommands
{
    private static bool TryChain(Entity session, out XafChain chain, out int index)
    {
        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
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

    /// <summary>Opens the SVG picker beside the chain inspector; the row chosen becomes the chain's art.</summary>
    public static void OpenArtPicker(Entity session)
    {
        if (!TryChain(session, out _, out _))
            return;

        ref AnimatorSvgMenu menu = ref session.GetComponent<AnimatorSvgMenu>();
        menu.Open = true;
        menu.ForChain = true;
        menu.Anchor = AnimatorLayout.ChainPickerAnchor(session.GetComponent<AnimatorUi>().ScreenSize);
    }

    /// <summary>Makes a picker row the selected chain's art and closes the picker, as one undo step.</summary>
    public static void SetArt(Entity session, AnimatorSvgChoice choice)
    {
        session.GetComponent<AnimatorSvgMenu>().Open = false;
        AnimatorHistoryCommands.Edit(session, () =>
        {
            int index = session.GetComponent<AnimatorSelection>().Chain;
            XafPackage package = session.GetComponent<AnimatorDocument>().Package;
            if (!XafChainAuthoring.SetArt(package, index, choice.Path, choice.Group))
            {
                AnimatorSession.SetStatus(session, "SELECT A CHAIN FIRST");
                return;
            }

            // The art is loaded when the visuals reload.
            AnimatorSession.RigChanged(session);
            AnimatorSession.SetStatus(session, $"ART {choice.Label}");
        });
    }

    /// <summary>The next bone clicked, in the preview or the tree, becomes the one named by <paramref name="pick"/>.</summary>
    public static void BeginPick(Entity session, AnimatorChainPick pick)
    {
        if (!TryChain(session, out XafChain chain, out _))
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
            XafPackage package = session.GetComponent<AnimatorDocument>().Package;
            int index = session.GetComponent<AnimatorSelection>().Chain;
            bool done = pick == AnimatorChainPick.Anchor
                ? XafChainAuthoring.SetBone(package, index, bone)
                : XafChainAuthoring.SetColliderBone(package, index, bone);
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

using Friflo.Engine.ECS;
using Godot;
using Xanadu.Animation;

namespace Xanadu.Tools.Animator.Commands;

/// <summary>
/// The two handles the selected chain shows in the preview: its first point and
/// the centre of its collider. Dragging one moves it along the bone it follows,
/// so the numbers in the chain inspector change with it.
/// </summary>
public static class AnimatorChainHandles
{
    /// <summary>How close the pointer has to be to grab a handle, in screen pixels.</summary>
    public const float HitRadius = 10f;

    public static bool IsChainDrag(AnimatorDragKind kind) =>
        kind is AnimatorDragKind.ChainRoot or AnimatorDragKind.ChainCollider;

    /// <summary>
    /// The selected chain's handles in rig space as drawn now: the first point,
    /// and the collider centre when it has one. False without a selected chain.
    /// </summary>
    public static bool TryGet(
        XafPackage package,
        int chain,
        Transform2D[] world,
        out Vector2 root,
        out Vector2? collider)
    {
        root = Vector2.Zero;
        collider = null;
        if (chain < 0 || chain >= package.Chains.Count)
            return false;

        XafChain selected = package.Chains[chain];
        Vector2? anchor = XafChainSimulation.Anchor(package, selected, world);
        if (anchor is null)
            return false;

        root = anchor.Value;
        collider = XafChainSimulation.Collider(package, selected, world);
        return true;
    }

    /// <summary>
    /// Starts dragging the handle under the pointer, the root first where they
    /// overlap. The whole drag is one undo step. False when no handle is hit.
    /// </summary>
    public static bool TryBegin(Entity session, Vector2 pointer, Transform2D placement, Transform2D[] world)
    {
        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        int index = session.GetComponent<AnimatorSelection>().Chain;
        if (!TryGet(package, index, world, out Vector2 root, out Vector2? collider))
            return false;

        AnimatorDragKind kind;
        if ((placement * root).DistanceTo(pointer) <= HitRadius)
            kind = AnimatorDragKind.ChainRoot;
        else if (collider is { } center && (placement * center).DistanceTo(pointer) <= HitRadius)
            kind = AnimatorDragKind.ChainCollider;
        else
            return false;

        AnimatorHistoryCommands.BeginGesture(session);
        session.GetComponent<AnimatorDrag>() = new AnimatorDrag { Kind = kind, StartPointer = pointer };
        string what = kind == AnimatorDragKind.ChainRoot ? "ROOT" : "COLLIDER";
        AnimatorSession.SetStatus(session, $"MOVING {package.Chains[index].Id} {what}");
        return true;
    }

    /// <summary>Moves the grabbed handle to the pointer, in the space of the bone it follows.</summary>
    public static void Drag(Entity session, Vector2 pointer, Transform2D placement)
    {
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        AnimatorDragKind kind = session.GetComponent<AnimatorDrag>().Kind;
        int index = session.GetComponent<AnimatorSelection>().Chain;
        XafPackage package = document.Package;
        if (index < 0 || index >= package.Chains.Count)
            return;

        XafChain chain = package.Chains[index];
        Transform2D[] world = XafRig.WorldBones(
            package, AnimatorGeometry.PreviewPose(document, session.GetComponent<AnimatorModes>()));
        int bone = XafRig.FindBone(package, kind == AnimatorDragKind.ChainRoot ? chain.BoneId : chain.CollideBoneId);
        if (bone < 0)
            return;

        Vector2 local = world[bone].AffineInverse() * (placement.AffineInverse() * pointer);
        if (kind == AnimatorDragKind.ChainRoot)
            XafChainAuthoring.SetOffset(package, index, local);
        else
            XafChainAuthoring.SetCollideOffset(package, index, local);
    }
}

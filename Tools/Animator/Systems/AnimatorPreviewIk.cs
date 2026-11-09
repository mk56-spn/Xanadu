using Friflo.Engine.ECS;
using Godot;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Tools.Animator.Commands;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>Solves the two-bone IK drag: the pointer pulls a joint, its parent and grandparent follow.</summary>
internal static class AnimatorPreviewIk
{
    /// <summary>
    /// Solves the two-bone chain for the pointer, folding the middle joint to
    /// its bend lock's side, or else the side it already bends to, then turns
    /// the grandparent and parent to match.
    /// </summary>
    public static void Pose(Entity session, Vector2 pointer, Transform2D placement, bool free)
    {
        ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
        int joint = session.GetComponent<AnimatorDrag>().Joint;
        if (!AnimatorPreviewChain.TryGet(document.Package, joint, out int parent, out int grandparent))
            return;

        XafPose pose = document.Runtime.Pose;
        Transform2D[] world = XafRig.WorldBones(document.Package, pose);
        Vector2 root = world[grandparent].Origin;
        Vector2 elbow = world[parent].Origin;
        Vector2 end = world[joint].Origin;
        Vector2 reach = end - root;
        bool bendPositive = XafJointLocks.BendPositive(document.Package, parent) ??
                            new Vector2(-reach.Y, reach.X).Dot(elbow - root) >= 0f;
        Vector2 target = placement.AffineInverse() * pointer;
        ref AnimatorSnapGuides guides = ref session.GetComponent<AnimatorSnapGuides>();
        guides = default;
        if (!free)
        {
            // The chain moves, so it only snaps to joints outside it; its root stays a target.
            float radius = AnimatorSnap.Pixels / Mathf.Max(0.0001f, Mathf.Abs(placement.Scale.X));
            if (session.GetComponent<AnimatorModes>().Align)
            {
                target = AnimatorSnap.AlignPoint(
                    target,
                    AnimatorSnap.Joints(document.Package, world, grandparent, keepRoot: true),
                    radius,
                    out guides);
            }
            if (AnimatorPreviewGrid.On(session))
                target = AnimatorSnap.ToGrid(target, guides);
        }

        Vector2[] solved = XafFabrik.Solve(new[] { root, elbow, end }, target, bendPositive);

        Aim(document.Package, pose, grandparent, parent, solved[1]);
        Aim(document.Package, pose, parent, joint, solved[2]);
        XafJointLocks.Apply(document.Package, pose);
    }

    /// <summary>Turns a bone about its origin so its child joint points at a skeleton-space target.</summary>
    private static void Aim(XafPackage package, XafPose pose, int bone, int child, Vector2 target) =>
        XafTargets.Aim(package, pose, bone, child, target);
}

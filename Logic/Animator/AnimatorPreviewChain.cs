using Xanadu.Model.Animation;

namespace Xanadu.Logic.Animator;

/// <summary>The joint's parent and grandparent, when it has both; the pair an IK drag turns.</summary>
public static class AnimatorPreviewChain
{
    public static bool TryGet(RigPackage package, int joint, out int parent, out int grandparent)
    {
        parent = package.Skeleton.Bones[joint].Parent;
        grandparent = parent >= 0 ? package.Skeleton.Bones[parent].Parent : -1;
        return grandparent >= 0;
    }
}

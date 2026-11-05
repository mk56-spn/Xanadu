using Friflo.Engine.ECS;
using Xanadu.Ecs;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>Whether the animator's SNAP toggle is on, asked the same way by every preview drag.</summary>
internal static class AnimatorPreviewGrid
{
    public static bool On(Entity session) => ViewportSnap.IsOn(session.Store);
}

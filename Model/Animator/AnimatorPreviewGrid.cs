using Friflo.Engine.ECS;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Model.Animator;

/// <summary>Whether the animator's SNAP toggle is on, asked the same way by every preview drag.</summary>
public static class AnimatorPreviewGrid
{
    /// <summary>Grid spacing in skeleton units (one unit is one pixel).</summary>
    public const float Cell = 20f;

    public static bool On(Entity session) => ViewportSnap.IsOn(session.Store);
}

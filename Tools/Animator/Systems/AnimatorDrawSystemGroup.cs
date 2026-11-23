using Friflo.Engine.ECS.Systems;
using Xanadu.Core.Maths;
using Xanadu.Drawers.Animator;
using Xanadu.Logic.Animator;
using Xanadu.Screens.Animator;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// Runs last: the grid, the chains settled on this frame's pose, the preview
/// art, then the timeline's header and body.
/// </summary>
public sealed class AnimatorDrawSystemGroup : SystemGroup
{
    public AnimatorDrawSystemGroup(
        IRectCanvas canvas,
        Handle gridCanvas,
        Handle previewCanvas,
        Handle timelineCanvas,
        AnimatorArt attachments,
        Handle heldCanvas) : base("Animator draw")
    {
        Add(new AnimatorGridDrawSystem(canvas, gridCanvas));
        Add(new AnimatorChainSimulationSystem());
        Add(new AnimatorPreviewDrawSystem(canvas, previewCanvas, attachments, heldCanvas));
        Add(new AnimatorTimelineHeaderSystem());
        Add(new AnimatorTimelineDrawSystem(canvas, timelineCanvas));
    }
}

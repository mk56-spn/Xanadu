using Friflo.Engine.ECS.Systems;
using Xanadu.Drawers.Animator;
using Xanadu.Model.Animator;
using Xanadu.Tools.Animator.Systems;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;
using Xanadu.Ui.Systems.Input;
using Xanadu.Ui.Systems.Ui;

namespace Xanadu.Tools.Animator;

/// <summary>
/// Animator-specific systems; run after the generic UI group. Commands first,
/// then input, playback, the panels and inspectors that show the result, and
/// drawing last.
/// </summary>
public sealed class AnimatorSystemGroup : SystemGroup
{
    public AnimatorSystemGroup(
        IRectCanvas canvas,
        Handle gridCanvas,
        Handle previewCanvas,
        Handle timelineCanvas,
        AnimatorArt attachments,
        IVisualItemFactory visuals,
        IPointerInputSource pointer,
        IWheelSource wheel,
        IKeyPressSource keys,
        IAnimatorClick? click = null,
        ToastSystem? toasts = null,
        Handle heldCanvas = default) : base("Animator")
    {
        Add(new AnimatorCommandSystemGroup(pointer, visuals));
        Add(new AnimatorInputSystemGroup(pointer, wheel, keys));
        Add(new AnimatorPlaybackSystemGroup(click));
        Add(new AnimatorPanelSystemGroup(pointer, visuals));
        Add(new AnimatorInspectorSystemGroup(pointer, toasts));
        Add(new AnimatorDrawSystemGroup(canvas, gridCanvas, previewCanvas, timelineCanvas, attachments, heldCanvas));
    }
}

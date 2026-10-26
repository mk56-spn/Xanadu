using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Writer.Systems.Canvas;
using Writer.Systems.Input;
using Writer.Tools.Animator.Systems;

namespace Writer.Tools.Animator;

/// <summary>Animator-specific systems; run after the generic UI group.</summary>
public sealed class AnimatorSystemGroup : SystemGroup
{
    public AnimatorSystemGroup(
        IRectCanvas canvas,
        Rid gridCanvas,
        Rid previewCanvas,
        Rid timelineCanvas,
        IAttachmentVisuals attachments,
        IVisualRidFactory visuals,
        IPointerInputSource pointer,
        IAnimatorWheel wheel,
        IKeyboardState keyboard) : base("Animator")
    {
        Add(new AnimatorActionSystem());
        Add(new AnimatorModeSystem());
        Add(new AnimatorPreviewInputSystem(pointer));
        Add(new AnimatorTimelineInputSystem(pointer, wheel));
        Add(new AnimatorShortcutSystem(keyboard));
        Add(new AnimatorPlaybackSystem());
        Add(new AnimatorHierarchySystem(visuals));
        Add(new AnimatorClipHeaderSystem());
        Add(new AnimatorInterpolationSystem());
        Add(new AnimatorFileHeaderSystem());
        Add(new AnimatorStatusSystem());
        Add(new AnimatorGridDrawSystem(canvas, gridCanvas));
        Add(new AnimatorPreviewDrawSystem(canvas, previewCanvas, attachments));
        Add(new AnimatorTimelineDrawSystem(canvas, timelineCanvas));
    }
}

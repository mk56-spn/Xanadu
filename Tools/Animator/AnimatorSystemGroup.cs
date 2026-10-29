using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Input;
using Xanadu.Tools.Animator.Files;
using Xanadu.Tools.Animator.Systems;

namespace Xanadu.Tools.Animator;

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
        IAnimatorSvgCatalog svgCatalog,
        IPointerInputSource pointer,
        IAnimatorWheel wheel,
        IKeyboardState keyboard) : base("Animator")
    {
        Add(new AnimatorClipConfigSystem(pointer));
        Add(new AnimatorActionSystem());
        Add(new AnimatorNewClipDialogSystem(pointer));
        Add(new AnimatorModeSystem());
        Add(new AnimatorBoneMenuSystem(pointer));
        Add(new AnimatorSvgMenuSystem(svgCatalog, visuals, pointer));
        Add(new AnimatorKeyMenuSystem(pointer));
        Add(new AnimatorPreviewInputSystem(pointer, wheel));
        Add(new AnimatorTimelineInputSystem(pointer, wheel));
        Add(new AnimatorShortcutSystem(keyboard));
        Add(new AnimatorSequenceSystem());
        Add(new AnimatorPlaybackSystem());
        Add(new AnimatorHierarchySystem(visuals, pointer));
        Add(new AnimatorClipHeaderSystem());
        Add(new AnimatorInspectorSystem());
        Add(new AnimatorChainInspectorSystem());
        Add(new AnimatorInterpolationSystem());
        Add(new AnimatorFileHeaderSystem());
        Add(new AnimatorStatusSystem());
        Add(new AnimatorTooltipSystem(pointer));
        Add(new AnimatorGridDrawSystem(canvas, gridCanvas));
        Add(new AnimatorChainSimulationSystem());
        Add(new AnimatorPreviewDrawSystem(canvas, previewCanvas, attachments));
        Add(new AnimatorTimelineDrawSystem(canvas, timelineCanvas));
    }
}

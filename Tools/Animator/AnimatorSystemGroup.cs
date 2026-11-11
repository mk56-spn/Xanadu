using Friflo.Engine.ECS.Systems;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Input;
using Xanadu.Systems.Ui;
using Xanadu.Tools.Animator.Files;
using Xanadu.Tools.Animator.Systems;

namespace Xanadu.Tools.Animator;

/// <summary>Animator-specific systems; run after the generic UI group.</summary>
public sealed class AnimatorSystemGroup : SystemGroup
{
    public AnimatorSystemGroup(
        IRectCanvas canvas,
        Handle gridCanvas,
        Handle previewCanvas,
        Handle timelineCanvas,
        IAttachmentVisuals attachments,
        IVisualRidFactory visuals,
        IAnimatorSvgCatalog svgCatalog,
        IPointerInputSource pointer,
        IWheelSource wheel,
        IKeyPressSource keys,
        IAnimatorClick? click = null,
        ToastSystem? toasts = null) : base("Animator")
    {
        Add(new AnimatorClipConfigSystem(pointer));
        Add(new AnimatorActionSystem());
        Add(new AnimatorNewClipDialogSystem(pointer));
        Add(new AnimatorInfoPanelSystem(pointer));
        Add(new XifEditorSystem(pointer, visuals));
        Add(new ParticleEditorSystem());
        Add(new AnimatorModeSystem());
        Add(new AnimatorBoneMenuSystem(pointer));
        Add(new AnimatorSvgMenuSystem(svgCatalog, visuals, pointer));
        Add(new AnimatorKeyMenuSystem(pointer));
        Add(new Xanadu.Systems.Groups.TimelineSystemGroup(pointer, wheel));
        Add(new AnimatorPreviewInputSystem(pointer, wheel));
        Add(new AnimatorShortcutSystem(keys));
        Add(new AnimatorSequenceSystem());
        Add(new AnimatorAttackCycleSystem(click));
        Add(new AnimatorPlaybackSystem());
        Add(new AnimatorPlantSystem());
        Add(new AnimatorTargetPanelSystem());
        Add(new AnimatorHierarchySystem(visuals, pointer));
        Add(new AnimatorRigPanelSystem());
        Add(new AnimatorLayerPanelSystem(visuals));
        Add(new AnimatorFlowSystem(visuals));
        Add(new AnimatorPreviewModeSystem());
        Add(new AnimatorEasingWindowSystem());
        Add(new EasePickerSystem());
        Add(new AnimatorClipHeaderSystem());
        Add(new AnimatorInspectorSystem());
        Add(new AnimatorChainInspectorSystem());
        Add(new AnimatorClothInspectorSystem());
        Add(new AnimatorBoneCardSystem());
        Add(new AnimatorValueSliderSystem());
        Add(new AnimatorSelfModulateSystem());
        Add(new AnimatorEventInspectorSystem());
        Add(new AnimatorFileHeaderSystem());
        Add(new AnimatorStatusSystem(toasts));
        Add(new AnimatorTooltipSystem(pointer));
        Add(new AnimatorGridDrawSystem(canvas, gridCanvas));
        Add(new AnimatorChainSimulationSystem());
        Add(new AnimatorSvgWatchSystem(svgCatalog));
        Add(new AnimatorPreviewDrawSystem(canvas, previewCanvas, attachments));
        Add(new AnimatorTimelineDrawSystem(canvas, timelineCanvas));
    }
}

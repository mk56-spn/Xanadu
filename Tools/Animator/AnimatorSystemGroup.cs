using Friflo.Engine.ECS.Systems;
using Xanadu.Model.Animation;
using Xanadu.Core.Maths;
using Xanadu.Tools.Animator.Ecs;
using Xanadu.Tools.Animator.Files;
using Xanadu.Tools.Animator.Systems;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;
using Xanadu.Ui.Systems.Groups;
using Xanadu.Ui.Systems.Input;
using Xanadu.Ui.Systems.Ui;

namespace Xanadu.Tools.Animator;

/// <summary>Animator-specific systems; run after the generic UI group.</summary>
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
        Add(new AnimatorClipConfigSystem(pointer));
        Add(new AnimatorActionSystem());
        Add(new AnimatorNewClipDialogSystem(pointer));
        Add(new AnimatorInfoPanelSystem(pointer));
        Add(new AnimatorShortcutsPanelSystem(pointer));
        Add(new AnimatorAutosaveSystem());
        Add(new AnimatorKeyTryoutSystem());
        Add(new XifEditorSystem(pointer, visuals));
        Add(new ParticleEditorSystem());
        Add(new AnimatorModeSystem());
        Add(new AnimatorBoneMenuSystem(pointer));
        Add(new AnimatorKeyMenuSystem(pointer));
        Add(new TimelineSystemGroup(pointer, wheel));
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
        Add(new AnimatorWorkspaceSystem());
        Add(new AnimatorEasingWindowSystem());
        Add(new EasePickerSystem());
        Add(new AnimatorClipHeaderSystem());
        Add(new AnimatorInspectorSystem());
        Add(new AnimatorChainInspectorSystem());
        Add(new AnimatorClothInspectorSystem());
        Add(new AnimatorBoneCardSystem());
        Add(new AnimatorValueFieldSystem());
        Add(new AnimatorEventInspectorSystem());
        Add(new AnimatorFileHeaderSystem());
        Add(new AnimatorStatusSystem(toasts));
        Add(new AnimatorTooltipSystem(pointer));
        Add(new AnimatorGridDrawSystem(canvas, gridCanvas));
        Add(new AnimatorChainSimulationSystem());
        Add(new AnimatorPreviewDrawSystem(canvas, previewCanvas, attachments, heldCanvas));
        Add(new AnimatorTimelineDrawSystem(canvas, timelineCanvas));
    }
}

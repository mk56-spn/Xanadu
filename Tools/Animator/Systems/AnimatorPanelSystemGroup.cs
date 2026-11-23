using Friflo.Engine.ECS.Systems;
using Xanadu.Screens.Animator;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Input;
using Xanadu.Ui.Systems.Ui;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// The shell's panels: target, hierarchy, rig, layers and flow, the preview and
/// workspace switches, and the easing window.
/// </summary>
public sealed class AnimatorPanelSystemGroup : SystemGroup
{
    public AnimatorPanelSystemGroup(IPointerInputSource pointer, IVisualItemFactory visuals) : base("Animator panels")
    {
        Add(new AnimatorTargetPanelSystem());
        Add(new AnimatorHierarchySystem(visuals, pointer));
        Add(new AnimatorRigPanelSystem());
        Add(new AnimatorLayerPanelSystem(visuals));
        Add(new AnimatorFlowSystem(visuals));
        Add(new AnimatorPreviewModeSystem());
        Add(new AnimatorWorkspaceSystem());
        Add(new AnimatorEasingWindowSystem());
        Add(new EasePickerSystem());
    }
}

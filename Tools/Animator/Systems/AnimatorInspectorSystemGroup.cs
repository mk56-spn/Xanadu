using Friflo.Engine.ECS.Systems;
using Xanadu.Screens.Animator;
using Xanadu.Ui.Systems.Input;
using Xanadu.Ui.Systems.Ui;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// Readouts of the selection and the file: the clip header, the inspectors and
/// bone card, value fields, the file header, status toasts and tooltips.
/// </summary>
public sealed class AnimatorInspectorSystemGroup : SystemGroup
{
    public AnimatorInspectorSystemGroup(IPointerInputSource pointer, ToastSystem? toasts) : base("Animator inspectors")
    {
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
    }
}

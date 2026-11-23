using Friflo.Engine.ECS.Systems;
using Xanadu.Logic.Animator;
using Xanadu.Screens.Animator;
using Xanadu.Ui.Systems.Groups;
using Xanadu.Ui.Systems.Input;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// Direct input: the mode switches, the bone and key context menus, the shared
/// timeline, the viewport's drags and wheel, and keyboard shortcuts.
/// </summary>
public sealed class AnimatorInputSystemGroup : SystemGroup
{
    public AnimatorInputSystemGroup(IPointerInputSource pointer, IWheelSource wheel, IKeyPressSource keys) : base("Animator input")
    {
        Add(new AnimatorModeSystem());
        Add(new AnimatorBoneMenuSystem(pointer));
        Add(new AnimatorKeyMenuSystem(pointer));
        Add(new TimelineSystemGroup(pointer, wheel));
        Add(new AnimatorPreviewInputSystem(pointer, wheel));
        Add(new AnimatorShortcutSystem(keys));
    }
}

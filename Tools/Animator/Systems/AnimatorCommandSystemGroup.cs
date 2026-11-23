using Friflo.Engine.ECS.Systems;
using Xanadu.Logic.Animator;
using Xanadu.Screens.Animator;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Input;
using Xanadu.Ui.Systems.Ui;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// Turns presses into commands and runs the work they start: the button intents,
/// the dialogs and popups that ask for a value, autosave, the key tryout and the
/// embedded Xif and particle editors.
/// </summary>
public sealed class AnimatorCommandSystemGroup : SystemGroup
{
    public AnimatorCommandSystemGroup(IPointerInputSource pointer, IVisualItemFactory visuals) : base("Animator commands")
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
    }
}

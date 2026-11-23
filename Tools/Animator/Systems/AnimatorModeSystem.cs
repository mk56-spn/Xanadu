using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Ecs;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// Keeps mode toggles and <see cref="AnimatorModes"/> in step. Whichever side
/// moved away from the last agreed value wins: a click on the toggle sets the
/// mode, and a mode changed by a command moves the toggle. A toggle shows only
/// while its mode applies (<see cref="AnimatorModes.Applies"/>): PIN CHILDREN
/// while EDIT RIG is on, ONION and PATHS while it is off, only ONION in PREVIEW.
/// </summary>
public sealed class AnimatorModeSystem : QuerySystem<Toggle, ModeToggle>
{
    // A hidden toggle still has to be found, to show it again.
    public AnimatorModeSystem() => Filter.WithDisabled();

    protected override void OnUpdate()
    {
        Entity session = AnimatorSession.Find(Query.Store);
        if (session.IsNull)
            return;

        AnimatorUi ui = session.GetComponent<AnimatorUi>();
        foreach (Entity entity in Query.ToEntityList())
        {
            ref Toggle toggle = ref entity.GetComponent<Toggle>();
            ref ModeToggle binding = ref entity.GetComponent<ModeToggle>();
            ref AnimatorModes modes = ref session.GetComponent<AnimatorModes>();

            if (toggle.Value != modes.Get(binding.Mode))
            {
                if (toggle.Value != binding.Synced)
                {
                    modes.Set(binding.Mode, toggle.Value);
                    Announce(session, binding.Mode, toggle.Value);
                }
                else
                {
                    toggle.Value = modes.Get(binding.Mode);
                }
            }

            binding.Synced = toggle.Value;
            bool preview = AnimatorPreviewModeCommands.IsOn(session);
            UiLayout.SetHidden(entity, !modes.Applies(binding.Mode, preview) ||
                                       (preview && Array.IndexOf(ui.HiddenInPreview ?? [], entity) >= 0));
        }
    }

    /// <summary>Says what the mode change means, the same way a click on its toggle does.</summary>
    public static void Announce(Entity session, AnimatorMode mode, bool value)
    {
        if (mode == AnimatorMode.Rest)
        {
            session.GetComponent<AnimatorDocument>().Runtime.SetPaused(true);
            AnimatorSession.SetStatus(session, value ? "EDITING THE RIG: CHANGES MOVE EVERY CLIP" : "ANIMATING");
        }
        else
        {
            if (mode == AnimatorMode.Parts)
            {
                AnimatorSession.SetStatus(session, value ? "PART PLACEMENT" : "BONE MOVEMENT");
                return;
            }

            string name = mode switch
            {
                AnimatorMode.AutoKey => "AUTO KEY",
                AnimatorMode.Ik => "IK DRAG",
                AnimatorMode.Pin => "PIN CHILDREN",
                AnimatorMode.Kites => "BONE KITES",
                AnimatorMode.Handles => "HANDLES",
                AnimatorMode.Align => "ALIGN TO JOINTS",
                AnimatorMode.FullKeys => "FULL KEY VIEW",
                _ => "ONION",
            };
            AnimatorSession.SetStatus(session, $"{name} {(value ? "ON" : "OFF")}");
        }
    }
}

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Logic.Animator.Commands;
using Xanadu.Model.Animator;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Screens.Animator;

/// <summary>
/// Keeps mode switches (<see cref="Switches"/>) and their subject's tags in step. Whichever side
/// moved away from the last agreed value wins: a click on the toggle sets the
/// mode, and a mode changed by a command moves the toggle. A toggle shows only
/// while its mode applies (<see cref="AnimatorModes.Applies"/>): PIN CHILDREN
/// while EDIT RIG is on, ONION and PATHS while it is off, only ONION in PREVIEW.
/// </summary>
public sealed class AnimatorModeSystem : QuerySystem<Toggle, Switches>
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
            // The layout builds its switches before there is a session: they act on it from here on.
            if (!entity.HasComponent<Subject>())
                CommandBuffer.AddComponent(entity.Id, new Subject(session));
            Entity subject = entity.TryGetComponent(out Subject link) && !link.Target.IsNull ? link.Target : session;

            ref Toggle toggle = ref entity.GetComponent<Toggle>();
            ref Switches binding = ref entity.GetComponent<Switches>();
            if (AnimatorModeTags.ModeOf(binding.Tags) is not { } mode)
                continue;

            bool on = subject.Tags.HasAll(binding.Tags);
            if (toggle.Value != on)
            {
                if (toggle.Value != binding.Synced)
                {
                    subject.SetTags(binding.Tags, toggle.Value);
                    AnimatorSessionCommands.AnnounceMode(subject, mode, toggle.Value);
                }
                else
                {
                    toggle.Value = on;
                }
            }

            binding.Synced = toggle.Value;
            bool preview = AnimatorPreviewMode.IsOn(session);
            UiLayout.SetHidden(entity, !AnimatorModes.Of(subject).Applies(mode, preview) ||
                                       (preview && Array.IndexOf(ui.HiddenInPreview ?? [], entity) >= 0));
        }
    }

}

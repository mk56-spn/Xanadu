using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Core.Timeline;
using Xanadu.Logic.Animator.Commands;
using Xanadu.Model.Animator;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Ui;

namespace Xanadu.Screens.Animator;

/// <summary>
/// Names the selected keys' curve on the EASING header button, and closes the
/// picker in preview mode, which hides the timeline it belongs to. The picker
/// itself is the shared one (<see cref="EasePickerSystem"/>).
/// </summary>
public sealed class AnimatorEasingWindowSystem : QuerySystem<AnimatorUi>
{
    protected override void OnUpdate()
    {
        foreach (Entity session in Query.ToEntityList())
        {
            AnimatorUi ui = session.GetComponent<AnimatorUi>();
            EasePreset? current = AnimatorEasePickerHost.CurrentPreset(session);
            UiText.Set(ui.EasingButton, current is { } set ? $"EASE: {EaseNames.Label(set)}" : "EASING");

            if (AnimatorPreviewMode.IsOn(session))
                AnimatorUi.Of(session).EasePicker.SetOpen(false);
        }
    }
}

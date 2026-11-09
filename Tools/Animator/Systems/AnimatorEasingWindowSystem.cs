using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Timeline;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// Names the selected keys' curve on the EASING header button, and closes the
/// picker in preview mode, which hides the timeline it belongs to. The picker
/// itself is the shared one (<see cref="Xanadu.Systems.Ui.EasePickerSystem"/>).
/// </summary>
public sealed class AnimatorEasingWindowSystem : QuerySystem<AnimatorEasingWindow, AnimatorUi>
{
    protected override void OnUpdate()
    {
        foreach (Entity session in Query.ToEntityList())
        {
            AnimatorUi ui = session.GetComponent<AnimatorUi>();
            EasePreset? current = AnimatorEasePickerHost.CurrentPreset(session);
            UiText.Set(ui.EasingButton, current is { } set ? $"EASE: {EaseNames.Label(set)}" : "EASING");

            if (AnimatorPreviewModeCommands.IsOn(session))
                session.GetComponent<AnimatorEasingWindow>().Open = false;
        }
    }
}

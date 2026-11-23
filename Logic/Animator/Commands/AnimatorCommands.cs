using System;
using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Model.Animator;
using Xanadu.Model.Animator.Intents;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Logic.Animator.Commands;

/// <summary>
/// Hears the animator's intents (<see cref="Xanadu.Model.Animator.Intents"/>)
/// on the session entity. Keys, buttons, menus and the CLI emit them there;
/// each command class registers the ones it owns in its own <c>Listen</c>.
/// </summary>
public static class AnimatorCommands
{
    /// <summary>Starts every owner hearing its intents on <paramref name="session"/>; once per session.</summary>
    public static void Listen(Entity session)
    {
        AnimatorArtCommands.Listen(session);
        AnimatorLayerCommands.Listen(session);
        AnimatorWeightCommands.Listen(session);
        AnimatorSessionCommands.Listen(session);
        AnimatorAttachmentCommands.Listen(session);
        AnimatorSequenceCommands.Listen(session);
        AnimatorAttackCycleCommands.Listen(session);
        AnimatorFlowCommands.Listen(session);
        AnimatorEffectCommands.Listen(session);
        AnimatorWorkspaceCommands.Listen(session);
        AnimatorPreviewModeCommands.Listen(session);
        AnimatorHistoryCommands.Listen(session);
        AnimatorKeyCommands.Listen(session);
        AnimatorNewClipDialogCommands.Listen(session);
        AnimatorKeyTryoutCommands.Listen(session);
        AnimatorClipCommands.Listen(session);
        AnimatorTargetCommands.Listen(session);
        AnimatorPlantCommands.Listen(session);
        AnimatorEditing.Listen(session);
        AnimatorEventCommands.Listen(session);
        AnimatorChainCommands.Listen(session);
        AnimatorChainPlacementCommands.Listen(session);
        AnimatorClothCommands.Listen(session);
        AnimatorPoseCommands.Listen(session);
        AnimatorFileCommands.Listen(session);
        AnimatorPreviewCommands.Listen(session);
        AnimatorTransformCommands.Listen(session);
        AnimatorKeyClipboardCommands.Listen(session);
        AnimatorOverrideCommands.Listen(session);
        ListenPanels(session);
    }

    /// <summary>
    /// Hears <typeparamref name="T"/> on the session: first drops the picks
    /// another intent withdraws (<see cref="AnimatorPendingPicks"/>), then
    /// refuses rig changes outside EDIT RIG (<see cref="IChangesRig"/>), then runs.
    /// </summary>
    public static void On<T>(this Entity session, Action<Entity, T> run)
        where T : struct, IIntent<T>
    {
        session.AddSignalHandler<T>(signal =>
        {
            Entity on = signal.Entity;
            AnimatorPendingPicks.Dismiss(on, signal.Event);
            if (signal.Event is IChangesRig && !AnimatorModes.Of(on).Rest)
            {
                AnimatorSession.SetStatus(on, "THAT CHANGES THE RIG · TURN ON EDIT RIG FIRST");
                return;
            }

            run(on, signal.Event);
        });
    }

    /// <summary>The popups and windows that only open or close.</summary>
    private static void ListenPanels(Entity session)
    {
        session.On<ToggleEasingWindow>((s, _) => AnimatorUi.Of(s).EasePicker.ToggleOpen());
        session.On<ToggleShortcuts>((s, _) => AnimatorUi.Of(s).ShortcutsPanel.ToggleOpen());
        session.On<OpenInfo>((s, _) => AnimatorUi.Of(s).InfoPanel.SetOpen(true));
        session.On<CloseInfo>((s, _) => AnimatorUi.Of(s).InfoPanel.SetOpen(false));
        session.On<OpenClipLength>((s, _) =>
        {
            AnimatorUi.Of(s).ClipLengthPanel.SetOpen(true);
            AnimatorSession.SetStatus(s, "TYPE A LENGTH OR STEP WITH THE ARROWS");
        });
        session.On<TogglePathWindow>((s, _) =>
        {
            bool window = !s.Tags.Has<PathWindow>();
            s.SetTag<PathWindow>(window);
            AnimatorSession.SetStatus(s, window
                ? "MOTION PATHS: ONE SECOND AROUND THE PLAYHEAD"
                : "MOTION PATHS: WHOLE CLIP");
        });
    }
}

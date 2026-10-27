using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Replays;
using Xanadu.Settings;
using Xanadu.Systems.Input;

namespace Xanadu.Systems.Replays;

/// <summary>
/// Notes each key press and release, stamped with the song time the play
/// systems will judge it against. Nothing is recorded during the countdown,
/// while paused, or after the song has ended.
/// </summary>
public sealed class ReplayRecordSystem(KeybindSettings keybinds, IKeyboardState keyboard)
    : QuerySystem<ReplayRecorder, Playhead, PlaySession>
{
    protected override void OnUpdate()
    {
        Query.ForEachEntity((ref recorder, ref playhead, ref session, entity) =>
        {
            if (!session.Started || session.Finished || entity.Tags.Has<PlayPaused>())
                return;

            for (int i = 0; i < KeybindSettings.Actions.Length; i++)
            {
                KeybindAction action = KeybindSettings.Actions[i];
                bool down = keyboard.IsPressed(keybinds.Get(action));
                if (down == recorder.Down[i])
                    continue;

                recorder.Down[i] = down;
                recorder.Events.Add(new ReplayEvent((int)playhead.TimeMs, action, down));
            }
        });
    }
}

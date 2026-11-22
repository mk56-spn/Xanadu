using Xanadu.Storage.Settings;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Replays;
using Xanadu.Model.Settings;
using Xanadu.Ui.Systems.Input;

namespace Xanadu.Logic.Replays;

/// <summary>
/// Notes each key press and release, stamped with the song time the play
/// systems will judge it against: when the key changed, where the keyboard knows. Nothing is recorded during the countdown,
/// while paused, or after the song has ended.
/// </summary>
public sealed class ReplayRecordSystem(
    KeybindSettings keybinds,
    IKeyboardState keyboard,
    OffsetSettings? offsets = null)
    : QuerySystem<ReplayRecorder, Playhead, PlaySession>
{
    private readonly OffsetSettings _offsets = offsets ?? new OffsetSettings(persist: false);

    protected override void OnUpdate() => Query.EachEntity(new Record(Query.Store, keybinds, keyboard, _offsets));

    private readonly record struct Record(EntityStore Store, KeybindSettings Keybinds, IKeyboardState Keyboard, OffsetSettings Offsets) : IEachEntity<ReplayRecorder, Playhead, PlaySession>
    {
        public void Execute(ref ReplayRecorder recorder, ref Playhead playhead, ref PlaySession session, int id)
        {
            Entity entity = Store.GetEntityById(id);

            if (!session.Started || session.Finished || entity.Tags.Has<PlayPaused>())
                return;

            int total = (int)PlayOffset.TotalMs(entity, Offsets.OffsetMs);
            if (recorder.Offsets.Count == 0 || recorder.Offsets[^1].TotalMs != total)
                recorder.Offsets.Add(new ReplayOffset((int)playhead.TimeMs, total));

            for (int i = 0; i < KeybindActions.All.Length; i++)
            {
                KeybindAction action = KeybindActions.All[i];
                Key key = Keybinds.Get(action);
                bool down = Keyboard.IsPressed(key);
                if (down == recorder.Down[i])
                    continue;

                recorder.Down[i] = down;
                recorder.Events.Add(new ReplayEvent((int)playhead.KeyMs(Keyboard.ChangedAt(key)), action, down));
            }
        }
    }
}

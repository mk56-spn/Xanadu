using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Settings;
using Xanadu.Storage.Settings;

namespace Xanadu.Logic.Replays;

/// <summary>
/// <c>--auto</c>: plays the run perfectly by pressing, on the
/// <see cref="ReplayKeyboard"/>, each note's key on its time, so hits and
/// their effects can be watched and shot without hands. It skips the song's entrance too. A dash takes the
/// next way in <see cref="DashWays"/>. Runs after the replay player.
/// </summary>
public sealed class AutoPlaySystem(KeybindSettings keybinds, ReplayKeyboard keyboard)
    : QuerySystem<Note>
{
    /// <summary>How long a press is held after the note's time, ms.</summary>
    private const long HoldMs = 40;

    /// <summary>The ways dashes go, in turn, by the dash's beat in the song.</summary>
    private static readonly KeybindAction[] DashWays =
    {
        KeybindAction.Up,
        KeybindAction.Left,
        KeybindAction.Right,
        KeybindAction.Down,
    };

    private readonly List<Key> _held = new();
    private readonly Dictionary<long, int> _dashTurns = new();

    protected override void OnUpdate()
    {
        long? now = null;
        _held.Clear();
        foreach (Entity entity in Query.Store.Query<Playhead, PlayfieldMode>().Entities)
        {
            if (entity.GetComponent<PlayfieldMode>().Kind != PlayfieldKind.Play)
                continue;
            now = entity.GetComponent<Playhead>().TimeMs;

            // Riding an orb: keep its key down to the tail.
            if (entity.TryGetComponent(out CurveBody body) && body.HoldOpen && now < body.Ride.EndMs)
                _held.Add(keybinds.Get(body.RideKey));

            // Skips the song's entrance, as a player would.
            if (entity.TryGetComponent(out PlaySession session) && session.Started &&
                session.SkipToMs >= 0 && !session.Skipped)
                entity.GetComponent<PlaySession>().SkipRequested = true;
        }

        if (now is not long time)
            return;

        foreach (var (notes, _) in Query.Chunks)
        {
            foreach (Note note in notes.Span)
            {
                if (time < note.TimeMs || time > note.TimeMs + HoldMs)
                    continue;

                KeybindAction action = note.Kind switch
                {
                    NoteKind.Dash => DashWayFor(note.TimeMs),
                    NoteKind.Entrance => KeybindAction.Action2,
                    NoteKind.Orb => KeybindAction.Action3,
                    _ => note.Target,
                };
                _held.Add(keybinds.Get(action));
            }
        }

        keyboard.Replay(_held);
    }

    /// <summary>Each dash gets the next way the first time it is seen, and keeps it.</summary>
    private KeybindAction DashWayFor(long timeMs)
    {
        if (!_dashTurns.TryGetValue(timeMs, out int turn))
        {
            turn = _dashTurns.Count;
            _dashTurns[timeMs] = turn;
        }

        return DashWays[turn % DashWays.Length];
    }
}

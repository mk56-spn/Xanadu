using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Playfields;
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
    [
        KeybindAction.Up,
        KeybindAction.Left,
        KeybindAction.Right,
        KeybindAction.Down,
    ];

    private readonly List<Key> _held = [];
    private readonly Dictionary<Key, long> _changedAt = [];
    private readonly Dictionary<long, int> _dashTurns = [];

    protected override void OnUpdate()
    {
        long? now = null;
        Playhead clock = default;
        CurveBody body = default;
        Playfield field = default;
        _held.Clear();
        _changedAt.Clear();
        Entity skipping = default;
        foreach (Entity entity in Query.Store.Query<Playhead, PlayfieldMode>().Entities)
        {
            if (entity.GetComponent<PlayfieldMode>().Kind != PlayfieldKind.Play)
                continue;
            clock = entity.GetComponent<Playhead>();
            now = clock.TimeMs;
            field = entity.GetComponent<Playfield>();

            // Riding an orb: keep its key down to the tail.
            if (entity.TryGetComponent(out body) && body.HoldOpen && now < body.Ride.EndMs)
                _held.Add(keybinds.Get(body.RideKey));

            // Skips the song's entrance, as a player would.
            if (entity.TryGetComponent(out PlaySession session) && session.Started &&
                session.SkipToMs >= 0 && !session.Skipped)
                skipping = entity;
        }

        // Asked after the loop: a skip seeks the playhead.
        if (!skipping.IsNull)
            skipping.EmitSignal(new SkipAsked());

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
                    NoteKind.Orb => OrbWayFor(note, body, field),
                    _ => note.Target,
                };
                Key key = keybinds.Get(action);
                _held.Add(key);
                // Pressed on the note's time itself, not on the frame that reached it.
                if (clock.TicksAt(note.TimeMs) is var ticks and not 0 && !_changedAt.ContainsKey(key))
                    _changedAt[key] = ticks;
            }
        }

        keyboard.Replay(_held, _changedAt);
    }

    /// <summary>The way a dash catches the orb's head from where the body is (Right when none does: it misses).</summary>
    private static KeybindAction OrbWayFor(in Note orb, in CurveBody body, in Playfield field)
    {
        foreach (KeybindAction way in DashWays)
        {
            if (Orbs.Catches(orb, orb.TimeMs, body.Position, PlayRules.DashDirection(way), field))
                return way;
        }

        return KeybindAction.Right;
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

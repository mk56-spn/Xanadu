using System;
using Friflo.Engine.ECS;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Playfields;
using Xanadu.Model.Scoring;
using Xanadu.Model.Settings;
using Xanadu.Systems.Input;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// Direction presses at a hit route's turns: the turn nearest in time, within
/// the miss window, takes the press when it is at a right angle to the way the
/// route runs there. The route turns at the turn's own time, however early or
/// late the press, so the corner sits where the chart puts it. A press the
/// same way or back the way it came is no turn and leaves it open; a turn never pressed carries straight on.
/// Turns are not scored.
/// </summary>
public sealed class PlayRouteTurns(PlayNoteContext context)
{
    private readonly ArchetypeQuery<Note, RouteRun> _running = context.Entities.Query<Note, RouteRun>();

    /// <summary>True when a turn took the press.</summary>
    public bool Press(KeybindAction action, Entity playfield)
    {
        RouteHeading heading = RouteHeadings.Of(action);
        if (heading == RouteHeading.None)
            return false;

        Playhead playhead = playfield.GetComponent<Playhead>();
        long judgedTime = playhead.TimeMs - PlayOffset.TotalMs(playfield, context.Offsets);
        long window = (long)ScoreRules.JudgementDeviation(Judgement.Miss);

        Entity best = default;
        int bestTurn = -1;
        long bestError = long.MaxValue;
        foreach (Entity entity in _running.ToEntityList())
        {
            Note note = entity.GetComponent<Note>();
            RouteRun run = entity.GetComponent<RouteRun>();
            if (note.Route is not { } route)
                continue;

            RoutePath path = RoutePath.Of(note, run.Start, run.Turns);
            for (int turn = 0; turn < route.Turns; turn++)
            {
                if (run.Pressed[turn])
                    continue;
                long error = Math.Abs(note.TimeMs + route.TimesMs[turn] - judgedTime);
                if (error > window || error >= bestError)
                    continue;

                // The way the route runs into this turn, with every earlier choice taken.
                RouteHeading into = HeadingOf(path.HeadingOf(turn));
                if (!RouteHeadings.Perpendicular(into, heading))
                    continue;

                best = entity;
                bestTurn = turn;
                bestError = error;
            }
        }

        if (bestTurn < 0)
            return false;

        ref RouteRun taken = ref best.GetComponent<RouteRun>();
        taken.Turns[bestTurn] = heading;
        taken.Pressed[bestTurn] = true;
        return true;
    }

    private static RouteHeading HeadingOf(Vector2 step) =>
        MathF.Abs(step.X) >= MathF.Abs(step.Y)
            ? step.X < 0 ? RouteHeading.Left : RouteHeading.Right
            : step.Y < 0 ? RouteHeading.Up : RouteHeading.Down;
}

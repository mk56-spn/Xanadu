using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Scoring;
using Xanadu.Model.Settings;
using Position = Xanadu.Core.Position;

namespace Xanadu.Logic.Playfields;

/// <summary>What a note hit does to the run: the score, and for a route note the steering, for a dash the teleport, for a hold the held tail.</summary>
public sealed class PlayNoteActions
{
    public bool Hit(
        ref ScoreState score,
        Entity noteEntity,
        long offsetMs,
        Entity playfield,
        KeybindAction action)
    {
        Note note = noteEntity.GetComponent<Note>();
        Judgement judgement = PlayScoring.Apply(ref score, offsetMs);
        PlayChart.Judge(noteEntity, judgement, offsetMs);

        // A route note's head is judged like any note; the direction key it was hit
        // with sets the way the route sets off, and the route then steers the body (CurvePlayerSystem).
        if (note.Route is { } route)
        {
            noteEntity.AddComponent(new RouteRun
            {
                Start = RouteHeadings.Of(action),
                Turns = new RouteHeading[route.Turns],
                Pressed = new bool[route.Turns],
            });
            noteEntity.AddTag<NoteSteering>();
        }
        else if (note.IsDash)
        {
            // The teleport waits for the note's time (CurvePlayerSystem), so an early hit still lands on the beat.
            noteEntity.AddComponent(new DashRun { Heading = RouteHeadings.Of(action) });
            noteEntity.AddTag<NoteSteering>();
        }
        else if (note.IsHold)
        {
            // The tail is judged on release, or as Fair if still held at its time (PlayHolds).
            noteEntity.AddComponent(new HoldRun { Key = action });
            noteEntity.AddTag<NoteHolding>();
        }
        else
            noteEntity.DeleteEntity();
        return true;
    }
}

using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Scoring;
using Xanadu.Settings;
using Xanadu.Systems.Characters;
using Xanadu.Systems.Physics;
using Xanadu.Systems.Input;
using Xanadu.Theme;
using Position = Xanadu.Ecs.Position;
using NVector2 = System.Numerics.Vector2;

namespace Xanadu.Systems.Playfields;

/// <summary>Marks notes whose miss window has passed and judges them.</summary>
public sealed class PlayNoteMisses(PlayNoteContext context)
{
    public void Mark(Entity playfield)
    {
        ref Playhead playhead = ref playfield.GetComponent<Playhead>();
        ref ScoreState score = ref playfield.GetComponent<ScoreState>();
        long judgedTime = playhead.TimeMs - PlayOffset.TotalMs(playfield, context.Offsets);

        if (playhead.DurationMs > 0 && playhead.TimeMs >= playhead.DurationMs)
            return;

        foreach (Entity entity in context.Entities.Query<Note>().ToEntityList())
        {
            if (entity.Tags.Has<NoteMissed>())
                continue;

            Note note = entity.GetComponent<Note>();
            // Contact barriers and channel-end markers are chart checks, not
            // scored notes.
            if (note.Kind is NoteKind.Contact or NoteKind.ChannelEnd)
                continue;

            long missWindow =
                (long)ScoreRules.JudgementDeviation(Judgement.Miss);
            if (note.TimeMs + missWindow >= judgedTime)
                continue;

            entity.AddTag<NoteMissed>();
            score.Combo = 0;
            score.HasJudgement = true;
            score.LastJudgement = Judgement.Miss;
            PlayfieldView view = playfield.GetComponent<PlayfieldView>();
            score.JudgementPosition = NoteRules.IsRail(note.Kind)
                ? SplitlineRail.ScreenPosition(
                    view,
                    playfield.GetComponent<Playfield>(),
                    note,
                    playhead.TimeMs)
                : view.PlayfieldToScreen(GroupOffset.Shown(entity, note.Position));
            score.JudgementTimeMs = playhead.TimeMs;
            PlayScoring.Count(ref score, Judgement.Miss);
        }
    }
}

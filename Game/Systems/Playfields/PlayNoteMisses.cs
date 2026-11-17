using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Scoring;
using Xanadu.Systems.Physics;
using Xanadu.Systems.Input;
using Position = Xanadu.Core.Position;

namespace Xanadu.Systems.Playfields;

/// <summary>Marks notes whose miss window has passed and judges them.</summary>
public sealed class PlayNoteMisses(PlayNoteContext context)
{
    private readonly ArchetypeQuery<Note> _open =
        context.Entities.Query<Note>().WithoutAnyTags(Tags.Get<NoteMissed, NoteSteering>());
    private readonly List<int> _late = [];

    public void Mark(Entity playfield)
    {
        ref Playhead playhead = ref playfield.GetComponent<Playhead>();
        ref ScoreState score = ref playfield.GetComponent<ScoreState>();
        long judgedTime = playhead.TimeMs - PlayOffset.TotalMs(playfield, context.Offsets);

        if (playhead.DurationMs > 0 && playhead.TimeMs >= playhead.DurationMs)
            return;

        _late.Clear();
        _open.EachEntity(new LateNotes(judgedTime, _late));

        foreach (int id in _late)
        {
            Entity entity = context.Entities.GetEntityById(id);
            Note note = entity.GetComponent<Note>();
            entity.AddTag<NoteMissed>();
            PlayChart.Judge(entity, Judgement.Miss, note.TimeMs - judgedTime);
            PlayScoring.Mark(Judgement.Miss, comboBroken: score.Combo > 0);
            score.Combo = 0;
            score.HasJudgement = true;
            score.LastJudgement = Judgement.Miss;
            PlayfieldView view = playfield.GetComponent<PlayfieldView>();
            score.JudgementPosition = view.PlayfieldToScreen(GroupOffset.Shown(entity, note.Position));
            score.JudgementTimeMs = playhead.TimeMs;
            PlayScoring.Count(ref score, Judgement.Miss);
        }
    }

    /// <summary>Scored notes still open whose miss window ended before <paramref name="JudgedTime"/>.</summary>
    private readonly record struct LateNotes(long JudgedTime, List<int> Late) : IEachEntity<Note>
    {
        private static readonly long MissWindow = (long)ScoreRules.JudgementDeviation(Judgement.Miss);

        public void Execute(ref Note note, int id)
        {
            // Contact barriers are chart checks, not scored notes.
            if (NoteRules.IsScored(note.Kind) && note.TimeMs + MissWindow < JudgedTime)
                Late.Add(id);
        }
    }
}

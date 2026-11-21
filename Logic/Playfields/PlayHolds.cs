using System;
using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Scoring;
using Xanadu.Model.Settings;
using Xanadu.Ui.Systems.Input;

namespace Xanadu.Logic.Playfields;

/// <summary>
/// Judges the tails of hold notes whose head was hit. Letting go is judged
/// against the tail's time like a press (too early is a miss); a key still down
/// when the tail comes is judged <see cref="HeldThrough"/> there and then, a
/// middle grade, so holding on never beats a well-timed release.
/// </summary>
public sealed class PlayHolds(PlayNoteContext context, IKeyboardState keyboard)
{
    /// <summary>The grade a tail gets when the key is still down at its time.</summary>
    public const Judgement HeldThrough = Judgement.Fair;

    private readonly ArchetypeQuery<Note, HoldRun> _held =
        context.Entities.Query<Note, HoldRun>().AllTags(Tags.Get<NoteHolding>());

    public void Update(Entity playfield)
    {
        ref Playhead playhead = ref playfield.GetComponent<Playhead>();
        ref ScoreState score = ref playfield.GetComponent<ScoreState>();
        long judgedTime = playhead.TimeMs - PlayOffset.TotalMs(playfield, context.Offsets.OffsetMs);
        long missWindow = (long)ScoreRules.JudgementDeviation(Judgement.Miss);

        foreach (Entity entity in _held.ToEntityList())
        {
            Note note = entity.GetComponent<Note>();
            HoldRun run = entity.GetComponent<HoldRun>();
            long offset = note.EndMs - judgedTime;
            bool down = keyboard.IsPressed(context.Keybinds.Get(run.Key));

            Judgement judgement;
            if (offset <= 0)
                judgement = down ? HeldThrough : ScoreRules.Judge(offset);
            else if (!down)
                judgement = offset > missWindow ? Judgement.Miss : ScoreRules.Judge(offset);
            else
                continue;

            PlayScoring.Apply(ref score, judgement, offset);
            PlayfieldView view = playfield.GetComponent<PlayfieldView>();
            score.JudgementPosition = view.PlayfieldToScreen(note.TailPosition);
            score.JudgementTimeMs = playhead.TimeMs;
            entity.DeleteEntity();
        }
    }
}

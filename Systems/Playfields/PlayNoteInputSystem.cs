using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Writer.Scoring;
using Writer.Systems.Input;

namespace Writer.Systems.Playfields;

/// <summary>Hits play notes inside the 8 ms timing window and awards score.</summary>
public sealed class PlayNoteInputSystem : QuerySystem<Note>
{
    private const float HitRadius = PlayfieldRenderer.NoteSize / 2;

    private readonly EntityStore _entities;
    private readonly IPointerInputSource _pointer;

    private bool _wasPressed;

    public PlayNoteInputSystem(EntityStore entities, IPointerInputSource pointer)
    {
        _entities = entities;
        _pointer = pointer;
    }

    protected override void OnUpdate()
    {
        PointerInput input = _pointer.Read();
        bool pressed = input.Pressed;

        if (pressed && !_wasPressed)
            TryHit(input.Position);

        _wasPressed = pressed;
        MarkMisses();
    }

    private void TryHit(Vector2 screenPosition)
    {
        Entity playfield = FindPlayfield();
        if (playfield.IsNull || playfield.Tags.Has<PlayPaused>())
            return;

        ref Playhead playhead = ref playfield.GetComponent<Playhead>();
        ref ScoreState score = ref playfield.GetComponent<ScoreState>();
        ref PlayfieldView view = ref playfield.GetComponent<PlayfieldView>();

        if (playhead.DurationMs > 0 && playhead.TimeMs >= playhead.DurationMs)
            return;

        Vector2 position = view.ScreenToPlayfield(screenPosition);
        long missWindow = (long)ScoreRules.JudgementDeviation(Judgement.Miss);
        Entity hit = default;
        long bestError = long.MaxValue;
        long bestOffset = 0;

        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
        {
            if (entity.Tags.Has<NoteMissed>())
                continue;

            Note note = entity.GetComponent<Note>();
            long offset = note.TimeMs - playhead.TimeMs;
            long error = Math.Abs(offset);
            if (error > missWindow)
                continue;

            if (MathF.Abs(note.Position.X - position.X) > HitRadius ||
                MathF.Abs(note.Position.Y - position.Y) > HitRadius)
                continue;

            if (error < bestError)
            {
                bestError = error;
                bestOffset = offset;
                hit = entity;
            }
        }

        if (!hit.IsNull)
            Hit(ref score, hit, bestOffset);
    }

    private static void Hit(
        ref ScoreState score,
        Entity noteEntity,
        long offsetMs)
    {
        Judgement judgement = ScoreRules.Judge(offsetMs);

        score.NotesHit++;
        score.HasJudgement = true;
        score.LastJudgement = judgement;
        score.LastOffsetMs = offsetMs;
        score.AccuracyPoints += ScoreRules.AccuracyFor(judgement);

        if (judgement == Judgement.Miss)
        {
            score.Combo = 0;
        }
        else
        {
            score.Combo++;
            score.MaxCombo = Math.Max(score.MaxCombo, score.Combo);
        }

        noteEntity.DeleteEntity();
    }

    private void MarkMisses()
    {
        Entity playfield = FindPlayfield();
        if (playfield.IsNull || playfield.Tags.Has<PlayPaused>())
            return;

        ref Playhead playhead = ref playfield.GetComponent<Playhead>();
        ref ScoreState score = ref playfield.GetComponent<ScoreState>();

        if (playhead.DurationMs > 0 && playhead.TimeMs >= playhead.DurationMs)
            return;

        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
        {
            if (entity.Tags.Has<NoteMissed>())
                continue;

            Note note = entity.GetComponent<Note>();
            long missWindow =
                (long)ScoreRules.JudgementDeviation(Judgement.Miss);
            if (note.TimeMs + missWindow >= playhead.TimeMs)
                continue;

            entity.AddTag<NoteMissed>();
            score.Combo = 0;
            score.HasJudgement = true;
            score.LastJudgement = Judgement.Miss;
        }
    }

    private Entity FindPlayfield()
    {
        foreach (Entity entity in _entities
                     .Query<Playhead, PlayfieldView, PlayfieldMode, ScoreState>()
                     .ToEntityList())
        {
            if (entity.GetComponent<PlayfieldMode>().Kind == PlayfieldKind.Play)
                return entity;
        }

        return default;
    }
}

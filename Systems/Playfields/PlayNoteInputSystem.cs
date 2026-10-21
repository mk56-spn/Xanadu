using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Writer.Scoring;
using Writer.Settings;
using Writer.Systems.Input;
using Position = Writer.Ecs.Position;

namespace Writer.Systems.Playfields;

/// <summary>
/// Hits notes when the player is over them and the note's target key is
/// pressed inside the timing window.
/// </summary>
public sealed class PlayNoteInputSystem : QuerySystem<Note>
{
    private readonly EntityStore _entities;
    private readonly KeybindSettings _keybinds;
    private readonly IKeyboardState _keyboard;
    private readonly HashSet<Key> _held = new();

    public PlayNoteInputSystem(
        EntityStore entities,
        KeybindSettings keybinds,
        IKeyboardState keyboard)
    {
        _entities = entities;
        _keybinds = keybinds;
        _keyboard = keyboard;
    }

    protected override void OnUpdate()
    {
        Entity playfield = FindPlayfield();
        if (playfield.IsNull || playfield.Tags.Has<PlayPaused>())
            return;

        if (playfield.HasComponent<PlaySession>())
        {
            PlaySession session = playfield.GetComponent<PlaySession>();
            if (!session.Started || session.Finished)
                return;
        }

        foreach (KeybindAction action in KeybindSettings.Actions)
        {
            Key key = _keybinds.Get(action);
            bool pressed = _keyboard.IsPressed(key);
            bool wasPressed = _held.Contains(key);

            if (pressed)
                _held.Add(key);
            else
                _held.Remove(key);

            if (pressed && !wasPressed)
                TryHit(action, playfield);
        }

        MarkMisses(playfield);
    }

    private void TryHit(KeybindAction action, Entity playfield)
    {
        ref Playhead playhead = ref playfield.GetComponent<Playhead>();
        ref ScoreState score = ref playfield.GetComponent<ScoreState>();
        ref PlayfieldView view = ref playfield.GetComponent<PlayfieldView>();
        Player player = playfield.GetComponent<Player>();
        Vector2 playerScreen = playfield.GetComponent<Position>().Value;

        if (playhead.DurationMs > 0 && playhead.TimeMs >= playhead.DurationMs)
            return;

        var playerRect = new Rect2(
            playerScreen - player.Size / 2,
            player.Size);
        long missWindow = (long)ScoreRules.JudgementDeviation(Judgement.Miss);
        Entity hit = default;
        long bestError = long.MaxValue;
        long bestOffset = 0;

        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
        {
            if (entity.Tags.Has<NoteMissed>())
                continue;

            Note note = entity.GetComponent<Note>();
            if (note.Target != action)
                continue;

            long offset = note.TimeMs - playhead.TimeMs;
            long error = Math.Abs(offset);
            if (error > missWindow)
                continue;

            if (NoteFade.Alpha(note, playhead.TimeMs) <= 0)
                continue;

            float noteSize = PlayfieldRenderer.NoteSize * view.Scale;
            Vector2 noteScreen = view.PlayfieldToScreen(note.Position);
            var noteRect = new Rect2(
                noteScreen - new Vector2(noteSize / 2, noteSize / 2),
                new Vector2(noteSize, noteSize));

            if (!playerRect.Intersects(noteRect))
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

        if (judgement != Judgement.Miss)
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

            if (score.TotalNotes > 0)
            {
                score.ScorePoints = Math.Min(
                    1.0,
                    score.ScorePoints + 1.0 / score.TotalNotes);
            }
        }

        noteEntity.DeleteEntity();
    }

    private void MarkMisses(Entity playfield)
    {
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
            if (entity.GetComponent<PlayfieldMode>().Kind == PlayfieldKind.Play &&
                entity.HasComponent<Player>() &&
                entity.HasComponent<Position>())
            {
                return entity;
            }
        }

        return default;
    }
}

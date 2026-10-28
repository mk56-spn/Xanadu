using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Scoring;
using Xanadu.Settings;
using Xanadu.Systems.Input;
using Xanadu.Theme;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// Hits notes when the player is within <see cref="NoteReach"/> of them and the
/// note's target key is pressed inside the timing window. Mines keep using the
/// character's damage box.
/// </summary>
public sealed class PlayNoteInputSystem : QuerySystem<Note>
{
    private readonly EntityStore _entities;
    private readonly KeybindSettings _keybinds;
    private readonly IKeyboardState _keyboard;
    private readonly OffsetSettings _offsets;
    private readonly HashSet<Key> _held = new();

    public PlayNoteInputSystem(
        EntityStore entities,
        KeybindSettings keybinds,
        IKeyboardState keyboard,
        OffsetSettings? offsets = null)
    {
        _entities = entities;
        _keybinds = keybinds;
        _keyboard = keyboard;
        _offsets = offsets ?? new OffsetSettings(persist: false);
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
            {
                if (!TryMine(action, playfield))
                    TryHit(action, playfield);
            }
        }

        MarkMisses(playfield);
    }

    private bool TryMine(KeybindAction action, Entity playfield)
    {
        ref Playhead playhead = ref playfield.GetComponent<Playhead>();
        ref ScoreState score = ref playfield.GetComponent<ScoreState>();
        ref PlayfieldView view = ref playfield.GetComponent<PlayfieldView>();
        Player player = playfield.GetComponent<Player>();
        Vector2 playerScreen = playfield.GetComponent<Position>().Value;
        long judgedTime = playhead.TimeMs - _offsets.OffsetMs;

        var playerRect = new Rect2(
            playerScreen - player.Size / 2,
            player.Size);
        float mineSize = PlayfieldRenderer.MineSize * view.Scale;
        long window = (long)ScoreRules.JudgementDeviation(Judgement.Miss);

        foreach (Entity entity in _entities.Query<Mine>().ToEntityList())
        {
            Mine mine = entity.GetComponent<Mine>();
            if (Math.Abs(mine.TimeMs - judgedTime) > window)
                continue;

            if (MineFade.Alpha(mine, playhead.TimeMs) <= 0)
                continue;

            Vector2 mineScreen = view.PlayfieldToScreen(mine.Position);
            var mineRect = new Rect2(
                mineScreen - new Vector2(mineSize / 2, mineSize / 2),
                new Vector2(mineSize, mineSize));
            if (!playerRect.Intersects(mineRect))
                continue;

            score.AccuracyPenalty +=
                ScoreRules.MineAccuracyPenalty * score.TotalNotes;
            score.Combo = 0;
            score.HasJudgement = true;
            score.LastJudgement = Judgement.Mine;
            score.JudgementPosition = mineScreen;
            score.JudgementTimeMs = playhead.TimeMs;

            _entities.CreateEntity(new HitTrail(
                mineScreen,
                Palette.DangerBright,
                KeybindSettings.KeyLabel(_keybinds.Get(action))));
            entity.DeleteEntity();
            return true;
        }

        return false;
    }

    private void TryHit(KeybindAction action, Entity playfield)
    {
        ref Playhead playhead = ref playfield.GetComponent<Playhead>();
        ref ScoreState score = ref playfield.GetComponent<ScoreState>();
        ref PlayfieldView view = ref playfield.GetComponent<PlayfieldView>();
        Vector2 playerScreen = playfield.GetComponent<Position>().Value;

        if (playhead.DurationMs > 0 && playhead.TimeMs >= playhead.DurationMs)
            return;

        long judgedTime = playhead.TimeMs - _offsets.OffsetMs;
        long missWindow = (long)ScoreRules.JudgementDeviation(Judgement.Miss);
        Entity hit = default;
        long bestError = long.MaxValue;
        long bestOffset = 0;

        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
        {
            if (entity.Tags.Has<NoteMissed>())
                continue;

            Note note = entity.GetComponent<Note>();
            if (note.Kind != NoteKind.Hit &&
                note.Kind != NoteKind.Contact &&
                note.Kind != NoteKind.CharacterSwitch)
            {
                continue;
            }

            if (note.Target != action)
                continue;

            long offset = note.TimeMs - judgedTime;
            long error = Math.Abs(offset);
            if (error > missWindow)
                continue;

            if (NoteFade.Alpha(note, judgedTime) <= 0)
                continue;

            Vector2 noteScreen = view.PlayfieldToScreen(note.Position);
            if (!NoteReach.Contains(playerScreen, view, noteScreen))
                continue;

            if (error < bestError)
            {
                bestError = error;
                bestOffset = offset;
                hit = entity;
            }
        }

        if (!hit.IsNull)
        {
            Note hitNote = hit.GetComponent<Note>();
            Vector2 hitPosition = view.PlayfieldToScreen(hitNote.Position);
            Color hitColor = GroupColor.For(hit, PlayfieldRenderer.ColorFor(hitNote));
            float hitRadius = PlayfieldRenderer.NoteSize * view.Scale / 2f;
            score.JudgementPosition = hitPosition;
            score.JudgementTimeMs = playhead.TimeMs;

            _entities.CreateEntity(new HitTrail(
                hitPosition,
                PlayfieldRenderer.ColorFor(hitNote.Target),
                KeybindSettings.KeyLabel(_keybinds.Get(hitNote.Target))));

            if (Hit(ref score, hit, bestOffset, playfield))
                _entities.CreateEntity(new NotePop(
                    hitNote,
                    hitPosition,
                    hitColor,
                    hitRadius,
                    PlayfieldRenderer.NotePopLifetime));
        }
    }

    private static bool Hit(
        ref ScoreState score,
        Entity noteEntity,
        long offsetMs,
        Entity playfield)
    {
        Note note = noteEntity.GetComponent<Note>();
        bool contactPass = true;

        if (note.Kind == NoteKind.CharacterSwitch)
            ApplyCharacter(playfield, note.Character);

        if (playfield.HasComponent<ContactCounters>() &&
            note.Kind == NoteKind.Contact)
        {
            ref ContactCounters counters =
                ref playfield.GetComponent<ContactCounters>();
            contactPass = counters.Consume(
                note.Target,
                Math.Max(1, note.ContactRequired));
        }

        if (!contactPass)
        {
            PlayScoring.Apply(
                ref score,
                (long)ScoreRules.JudgementDeviation(Judgement.Miss) + 1);
            noteEntity.DeleteEntity();
            return false;
        }

        PlayScoring.Apply(ref score, offsetMs);
        if (note.Kind == NoteKind.Hit &&
            playfield.HasComponent<ContactCounters>())
        {
            playfield.GetComponent<ContactCounters>().Add(note.Target);
        }

        noteEntity.DeleteEntity();
        return true;
    }

    private static void ApplyCharacter(Entity playfield, CharacterKind kind)
    {
        if (!playfield.HasComponent<PlayerCharacter>())
            return;

        playfield.GetComponent<PlayerCharacter>().Kind = kind;
        playfield.RemoveTag<DashMovement>();
        playfield.RemoveTag<ContactCharacter>();

        switch (kind)
        {
            case CharacterKind.Contact:
                playfield.AddTag<DashMovement>();
                playfield.AddTag<ContactCharacter>();
                if (!playfield.HasComponent<ContactCounters>())
                    playfield.AddComponent(new ContactCounters());
                break;
            case CharacterKind.SplitLine:
                playfield.AddTag<DashMovement>();
                break;
        }
    }

    private void MarkMisses(Entity playfield)
    {
        ref Playhead playhead = ref playfield.GetComponent<Playhead>();
        ref ScoreState score = ref playfield.GetComponent<ScoreState>();
        long judgedTime = playhead.TimeMs - _offsets.OffsetMs;

        if (playhead.DurationMs > 0 && playhead.TimeMs >= playhead.DurationMs)
            return;

        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
        {
            if (entity.Tags.Has<NoteMissed>())
                continue;

            Note note = entity.GetComponent<Note>();
            long missWindow =
                (long)ScoreRules.JudgementDeviation(Judgement.Miss);
            if (note.TimeMs + missWindow >= judgedTime)
                continue;

            entity.AddTag<NoteMissed>();
            score.Combo = 0;
            score.HasJudgement = true;
            score.LastJudgement = Judgement.Miss;
            score.JudgementPosition = playfield
                .GetComponent<PlayfieldView>()
                .PlayfieldToScreen(note.Position);
            score.JudgementTimeMs = playhead.TimeMs;
            PlayScoring.Count(ref score, Judgement.Miss);
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

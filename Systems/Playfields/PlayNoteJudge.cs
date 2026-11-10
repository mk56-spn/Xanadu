using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Scoring;
using Xanadu.Settings;
using Xanadu.Systems.Characters;
using Xanadu.Systems.Physics;
using Xanadu.Systems.Input;
using Xanadu.Theme;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// Picks what an action key press should judge: the nearest matching action
/// note in reach and in the window, or a mine the player touched.
/// </summary>
public sealed class PlayNoteJudge(PlayNoteContext context, PlayNoteHits hits)
{
    public bool Mine(KeybindAction action, Entity playfield)
    {
        ref Playhead playhead = ref playfield.GetComponent<Playhead>();
        ref ScoreState score = ref playfield.GetComponent<ScoreState>();
        ref PlayfieldView view = ref playfield.GetComponent<PlayfieldView>();
        Player player = playfield.GetComponent<Player>();
        Vector2 playerScreen = playfield.GetComponent<Position>().Value;
        long judgedTime = playhead.TimeMs - PlayOffset.TotalMs(playfield, context.Offsets);

        var playerRect = new Rect2(
            playerScreen - player.Size / 2,
            player.Size);
        float mineSize = PlayfieldRenderer.MineSize * view.Scale;
        long window = (long)ScoreRules.JudgementDeviation(Judgement.Miss);

        foreach (Entity entity in context.Entities.Query<Mine>().ToEntityList())
        {
            Mine mine = entity.GetComponent<Mine>();
            if (Math.Abs(mine.TimeMs - judgedTime) > window)
                continue;

            if (MineFade.Alpha(mine, playhead.TimeMs) <= 0)
                continue;

            Vector2 mineScreen = view.PlayfieldToScreen(GroupOffset.Shown(entity, mine.Position));
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

            context.Entities.CreateEntity(
                new HitTrail(
                    mineScreen,
                    Palette.DangerBright,
                    KeybindSettings.KeyLabel(context.Keybinds.Get(action))),
                EffectLayer.From(entity));
            entity.DeleteEntity();
            return true;
        }

        return false;
    }

    public void Note(KeybindAction action, Entity playfield)
    {
        ref Playhead playhead = ref playfield.GetComponent<Playhead>();
        ref ScoreState score = ref playfield.GetComponent<ScoreState>();
        ref PlayfieldView view = ref playfield.GetComponent<PlayfieldView>();
        Vector2 playerScreen = playfield.GetComponent<Position>().Value;

        if (playhead.DurationMs > 0 && playhead.TimeMs >= playhead.DurationMs)
            return;

        long judgedTime = playhead.TimeMs - PlayOffset.TotalMs(playfield, context.Offsets);
        long missWindow = (long)ScoreRules.JudgementDeviation(Judgement.Miss);
        CharacterKind? character = playfield.TryGetComponent(
            out PlayerCharacter playerCharacter)
            ? playerCharacter.Kind
            : null;

        Entity hit = default;
        Note hitNote = default;
        long bestError = long.MaxValue;
        long bestOffset = 0;

        foreach (Entity entity in context.Entities.Query<Note>().ToEntityList())
        {
            if (entity.Tags.Has<NoteMissed>())
                continue;

            Note note = entity.GetComponent<Note>();
            if (!NoteRules.IsAction(note.Kind) || !NoteRules.Matches(note, action))
                continue;

            long offset = note.TimeMs - judgedTime;
            long error = Math.Abs(offset);
            if (error > missWindow || error >= bestError)
                continue;

            if (NoteFade.Alpha(note, judgedTime) <= 0)
                continue;

            Vector2 noteScreen = view.PlayfieldToScreen(GroupOffset.Shown(entity, note.Position));
            if (!NoteReach.Contains(playerScreen, view, noteScreen, character))
                continue;

            bestError = error;
            bestOffset = offset;
            hit = entity;
            hitNote = note;
        }

        if (hit.IsNull)
            return;

        hits.Note(
            action,
            hit,
            hitNote,
            bestOffset,
            playfield,
            ref score,
            view,
            playerScreen,
            playhead.TimeMs);
    }
}

using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Scoring;
using Xanadu.Model.Settings;
using Xanadu.Systems.Input;
using Xanadu.Ui.Ecs;
using Position = Xanadu.Core.Position;
using Xanadu.Model.Playfields;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// Picks what an action key press should judge: the nearest matching action
/// note in reach and in the window, or a mine the player touched.
/// </summary>
public sealed class PlayNoteJudge(PlayNoteContext context, PlayNoteHits hits)
{
    private readonly ArchetypeQuery<Note> _open =
        context.Entities.Query<Note>().WithoutAnyTags(Tags.Get<NoteMissed, NoteSteering>());

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
        float mineSize = PlayfieldMetrics.MineSize * view.Scale;
        long window = (long)ScoreRules.JudgementDeviation(Judgement.Miss);

        TouchedMine touched = context.Entities.Cached<Mine>().EachEntity(
            new TouchedMine(context.Entities, view, playerRect, judgedTime, playhead.TimeMs, mineSize, window));
        if (touched.Id == 0)
            return false;

        Entity entity = context.Entities.GetEntityById(touched.Id);
        score.AccuracyPenalty +=
            ScoreRules.MineAccuracyPenalty * score.TotalNotes;
        PlayScoring.Mark(Judgement.Mine, comboBroken: score.Combo > 0);
        score.Combo = 0;
        score.HasJudgement = true;
        score.LastJudgement = Judgement.Mine;
        score.JudgementPosition = touched.Screen;
        score.JudgementTimeMs = playhead.TimeMs;

        context.Entities.CreateEntity(
            new HitTrail(
                touched.Screen,
                Palette.DangerBright,
                KeybindSettings.KeyLabel(context.Keybinds.Get(action))),
            EffectLayer.From(entity));
        entity.DeleteEntity();
        return true;
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

        NearestNote nearest = _open.EachEntity(
            new NearestNote(context.Entities, view, action, playerScreen, character, judgedTime, missWindow));
        if (nearest.Id == 0)
            return;

        Entity hit = context.Entities.GetEntityById(nearest.Id);
        Note hitNote = hit.GetComponent<Note>();
        long bestOffset = nearest.Offset;


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

    /// <summary>The first live mine in the window whose box overlaps the player.</summary>
    private struct TouchedMine(
        EntityStore store,
        PlayfieldView view,
        Rect2 playerRect,
        long judgedTime,
        long playheadTime,
        float mineSize,
        long window) : IEachEntity<Mine>
    {
        public int Id;
        public Vector2 Screen;

        public void Execute(ref Mine mine, int id)
        {
            if (Id != 0 || Math.Abs(mine.TimeMs - judgedTime) > window)
                return;

            if (MineFade.Alpha(mine, playheadTime) <= 0)
                return;

            Vector2 mineScreen = view.PlayfieldToScreen(GroupOffset.Shown(store.GetEntityById(id), mine.Position));
            var mineRect = new Rect2(
                mineScreen - new Vector2(mineSize / 2, mineSize / 2),
                new Vector2(mineSize, mineSize));
            if (!playerRect.Intersects(mineRect))
                return;

            Id = id;
            Screen = mineScreen;
        }
    }

    /// <summary>The open action note matching the key that is nearest in time, in reach and visible.</summary>
    private struct NearestNote(
        EntityStore store,
        PlayfieldView view,
        KeybindAction action,
        Vector2 playerScreen,
        CharacterKind? character,
        long judgedTime,
        long missWindow) : IEachEntity<Note>
    {
        public int Id;
        public long Offset;
        private long _bestError = long.MaxValue;

        public void Execute(ref Note note, int id)
        {
            if (!NoteRules.IsAction(note.Kind) || !NoteRules.Matches(note, action))
                return;

            long offset = note.TimeMs - judgedTime;
            long error = Math.Abs(offset);
            if (error > missWindow || error >= _bestError)
                return;

            if (NoteFade.Alpha(note, judgedTime) <= 0)
                return;

            Vector2 noteScreen = view.PlayfieldToScreen(GroupOffset.Shown(store.GetEntityById(id), note.Position));
            if (!NoteReach.Contains(playerScreen, view, noteScreen, character))
                return;

            _bestError = error;
            Offset = offset;
            Id = id;
        }
    }
}

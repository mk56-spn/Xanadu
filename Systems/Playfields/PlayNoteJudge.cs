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

/// <summary>
/// Picks the note a key press should judge: the nearest matching note in the
/// window, a rail tap while a side is absorbed, or a mine the player touched.
/// </summary>
public sealed class PlayNoteJudge(PlayNoteContext context, PlayNoteHits hits)
{
    public bool RailHit(KeybindAction action, Entity playfield)
    {
        ref Playhead playhead = ref playfield.GetComponent<Playhead>();
        ref ScoreState score = ref playfield.GetComponent<ScoreState>();
        ref PlayfieldView view = ref playfield.GetComponent<PlayfieldView>();
        long judgedTime = playhead.TimeMs - PlayOffset.TotalMs(playfield, context.Offsets);
        long window = (long)ScoreRules.JudgementDeviation(Judgement.Miss);
        Entity hit = default;
        Note hitNote = default;
        long bestError = long.MaxValue;

        foreach (Entity entity in context.Entities.Query<Note>().ToEntityList())
        {
            if (entity.Tags.Has<NoteMissed>())
                continue;

            Note note = entity.GetComponent<Note>();
            if (!NoteRules.IsRail(note.Kind) ||
                note.ChannelSide != NoteRules.SideOf(action) ||
                note.PairIndex != NoteRules.PairIndexFor(action))
            {
                continue;
            }

            long error = Math.Abs(note.TimeMs - judgedTime);
            if (error > window || NoteFade.Alpha(note, judgedTime) <= 0)
                continue;

            if (error >= bestError)
                continue;

            bestError = error;
            hit = entity;
            hitNote = note;
        }

        if (hit.IsNull)
            return false;

        Vector2 position = SplitlineRail.ScreenPosition(
            view,
            playfield.GetComponent<Playfield>(),
            hitNote,
            playhead.TimeMs);
        float radius = SplitlineRail.NoteRadius * view.Scale;
        Color color = GroupColor.For(hit, PlayfieldRenderer.ColorFor(hitNote));

        score.JudgementPosition = position;
        score.JudgementTimeMs = playhead.TimeMs;
        PlayScoring.Apply(ref score, hitNote.TimeMs - judgedTime);

        context.Entities.CreateEntity(
            new HitTrail(
                position,
                PlayfieldRenderer.ColorFor(hitNote),
                KeybindSettings.KeyLabel(context.Keybinds.Get(action))),
            EffectLayer.From(hit));
        HitEffects.Spawn(context.Entities, playfield, hit, hitNote, position, color, radius);

        if (hitNote.HoldMs > 0)
        {
            context.Entities.CreateEntity(new RailHold(
                action,
                hitNote.ChannelSide,
                hitNote.PairIndex,
                hitNote.TimeMs,
                hitNote.TimeMs + hitNote.HoldMs,
                playfield));
        }

        hit.DeleteEntity();
        return true;
    }

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
        bool armed = character == CharacterKind.Armed;
        ArmedWeapon weapon = playfield.TryGetComponent(out ArmedWeapon equipped)
            ? equipped
            : new ArmedWeapon(WeaponKind.Spear);
        bool facesLeft = playfield.TryGetComponent(out CharacterFacing facing) &&
                         facing.Left;

        Entity hit = default;
        Note hitNote = default;
        long bestError = long.MaxValue;
        long bestOffset = 0;

        foreach (Entity entity in context.Entities.Query<Note>().ToEntityList())
        {
            if (entity.Tags.Has<NoteMissed>())
                continue;

            Note note = entity.GetComponent<Note>();
            if (!NoteRules.IsKeyed(note.Kind) ||
                !NoteRules.ActiveFor(note.Kind, character) ||
                !NoteRules.Matches(note, action) ||
                !PlayNoteActions.CanFormStep(playfield, note))
            {
                continue;
            }

            long offset = note.TimeMs - judgedTime;
            long error = Math.Abs(offset);
            if (error > missWindow)
                continue;

            if (NoteFade.Alpha(note, judgedTime) <= 0)
                continue;

            Vector2 noteScreen = view.PlayfieldToScreen(GroupOffset.Shown(entity, note.Position));
            bool inRange = armed && (note.Kind is NoteKind.Hit or NoteKind.Pair)
                ? WeaponHitRules.Contains(
                    weapon.Kind,
                    playerScreen,
                    facesLeft,
                    noteScreen,
                    view.Scale)
                : NoteReach.Contains(playerScreen, view, noteScreen, character);
            if (!inRange)
                continue;

            if (error >= bestError)
                continue;

            bestError = error;
            bestOffset = offset;
            hit = entity;
            hitNote = note;
        }

        if (hit.IsNull)
            return;

        if (armed && (hitNote.Kind is NoteKind.Hit or NoteKind.Pair))
        {
            hits.Swing(
                action,
                hit,
                hitNote,
                playfield,
                ref score,
                view,
                playerScreen,
                judgedTime,
                playhead.TimeMs,
                weapon.Kind,
                facesLeft);
            return;
        }

        hits.Note(
            action,
            hit,
            hitNote,
            bestOffset,
            playfield,
            ref score,
            view,
            playerScreen,
            playhead.TimeMs,
            triggerAttack: true);
    }
}

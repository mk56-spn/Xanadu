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

/// <summary>Applies a judged action-note hit: the trail, the judgement and the effects.</summary>
public sealed class PlayNoteHits(PlayNoteContext context, PlayNoteActions actions)
{
    public bool Note(
        KeybindAction action,
        Entity noteEntity,
        Note note,
        long offsetMs,
        Entity playfield,
        ref ScoreState score,
        PlayfieldView view,
        Vector2 playerScreen,
        long playheadTimeMs)
    {
        Vector2 hitPosition = view.PlayfieldToScreen(GroupOffset.Shown(noteEntity, note.Position));
        Color hitColor = GroupColor.For(noteEntity, PlayfieldRenderer.ColorFor(note));
        float hitRadius = PlayfieldRenderer.NoteSize * view.Scale / 2f;
        score.JudgementPosition = hitPosition;
        score.JudgementTimeMs = playheadTimeMs;

        context.Entities.CreateEntity(
            new HitTrail(
                hitPosition,
                PlayfieldRenderer.ColorFor(note),
                KeybindSettings.KeyLabel(context.Keybinds.Get(action))),
            EffectLayer.From(noteEntity));

        if (!actions.Hit(ref score, noteEntity, offsetMs, playfield, action))
            return false;

        FaceHit(playfield, hitPosition.X, playerScreen.X);
        GroundStepSystem.OnHit(playfield, note.TimeMs, playheadTimeMs + offsetMs, hitPosition.X, playerScreen.X);

        HitGhostSystem.Spawn(context.Entities, playfield);
        CharacterAttackSystem.Trigger(playfield, hitPosition.Y, playerScreen.Y);

        HitEffects.Spawn(context.Entities, playfield, noteEntity, note, hitPosition, hitColor, hitRadius);
        return true;
    }

    /// <summary>
    /// Turns the character towards a note it just hit, the way a horizontal dash
    /// turns it towards the dash. A note directly above or below leaves the
    /// facing alone.
    /// </summary>
    private static void FaceHit(Entity playfield, float noteScreenX, float playerScreenX)
    {
        float dx = noteScreenX - playerScreenX;
        if (dx == 0f)
            return;

        if (!playfield.HasComponent<CharacterFacing>())
            playfield.AddComponent(new CharacterFacing());

        playfield.GetComponent<CharacterFacing>().Left = dx < 0f;
    }
}

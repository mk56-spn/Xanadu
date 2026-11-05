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

/// <summary>Applies a judged hit: the trail, the judgement, the swing's extra targets and the effects.</summary>
public sealed class PlayNoteHits(PlayNoteContext context, PlayNoteActions actions)
{
    /// <summary>
    /// Armed's swing: the first note matches the key and the weapon hitbox,
    /// then every ordinary note at the same timestamp inside that hitbox is
    /// struck too, even when its own key does not match.
    /// </summary>
    public void Swing(
        KeybindAction action,
        Entity firstEntity,
        Note firstNote,
        Entity playfield,
        ref ScoreState score,
        PlayfieldView view,
        Vector2 playerScreen,
        long judgedTime,
        long playheadTimeMs,
        WeaponKind weapon,
        bool facesLeft)
    {
        var targets = new List<(Entity Entity, Note Note)>
        {
            (firstEntity, firstNote),
        };

        foreach (Entity entity in context.Entities.Query<Note>().ToEntityList())
        {
            if (entity.Id == firstEntity.Id || entity.Tags.Has<NoteMissed>())
                continue;

            Note note = entity.GetComponent<Note>();
            if (note.Kind is not (NoteKind.Hit or NoteKind.Pair) ||
                note.TimeMs != firstNote.TimeMs ||
                NoteFade.Alpha(note, judgedTime) <= 0)
            {
                continue;
            }

            Vector2 noteScreen = view.PlayfieldToScreen(note.Position);
            if (!WeaponHitRules.Contains(
                    weapon,
                    playerScreen,
                    facesLeft,
                    noteScreen,
                    view.Scale))
            {
                continue;
            }

            targets.Add((entity, note));
        }

        bool attackTriggered = false;
        foreach ((Entity entity, Note note) in targets)
        {
            bool hit = Note(
                action,
                entity,
                note,
                note.TimeMs - judgedTime,
                playfield,
                ref score,
                view,
                playerScreen,
                playheadTimeMs,
                triggerAttack: !attackTriggered);
            attackTriggered |= hit;
        }

        ApplyWeaponEffect(playfield, weapon, facesLeft);
    }

    public bool Note(
        KeybindAction action,
        Entity noteEntity,
        Note note,
        long offsetMs,
        Entity playfield,
        ref ScoreState score,
        PlayfieldView view,
        Vector2 playerScreen,
        long playheadTimeMs,
        bool triggerAttack)
    {
        Vector2 hitPosition = view.PlayfieldToScreen(note.Position);
        Color hitColor = GroupColor.For(noteEntity, PlayfieldRenderer.ColorFor(note));
        float hitRadius = PlayfieldRenderer.NoteSize * view.Scale / 2f;
        score.JudgementPosition = hitPosition;
        score.JudgementTimeMs = playheadTimeMs;

        context.Entities.CreateEntity(new HitTrail(
            hitPosition,
            PlayfieldRenderer.ColorFor(note),
            KeybindSettings.KeyLabel(context.Keybinds.Get(action))));

        if (!actions.Hit(ref score, noteEntity, offsetMs, playfield, action))
            return false;

        FaceHit(playfield, hitPosition.X, playerScreen.X);

        if (triggerAttack)
        {
            HitGhostSystem.Spawn(context.Entities, playfield);
            CharacterAttackSystem.Trigger(playfield, hitPosition.Y, playerScreen.Y);
        }

        HitEffects.Spawn(context.Entities, playfield, note, hitPosition, hitColor, hitRadius);
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

    private static void ApplyWeaponEffect(
        Entity playfield,
        WeaponKind weapon,
        bool facesLeft)
    {
        if (!playfield.TryGetComponent(out PhysicsBody body))
            return;

        if (weapon == WeaponKind.Spear)
        {
            NVector2 velocity = body.Body.LinearVelocity;
            if (velocity.Y > 0f)
                body.Body.SetLinearVelocity(new NVector2(velocity.X, 0f));
            return;
        }

        float direction = facesLeft ? 1f : -1f;
        float deltaVelocity =
            WeaponHitRules.AxeKnockbackSpeed / Box2DPhysicsWorld.PixelsPerMeter;
        body.Body.ApplyLinearImpulseToCenter(
            new NVector2(direction * deltaVelocity * body.Body.Mass, 0f),
            true);
    }
}

using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Playfields;
using Xanadu.Model.Settings;
using Position = Xanadu.Core.Position;

namespace Xanadu.Logic.Playfields;

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
        double shownMs)
    {
        Vector2 hitPosition = view.PlayfieldToScreen(Chords.Landing(context.Entities, note));
        Color hitColor = PlayfieldColors.ColorFor(note);
        float hitRadius = PlayfieldMetrics.NoteSize * view.Scale / 2f;
        score.JudgementPosition = hitPosition;
        score.JudgementTimeMs = shownMs;

        // Read before the hit takes the note away.
        int chartIndex = ListIndex.Of(noteEntity);
        if (!actions.Hit(ref score, noteEntity, offsetMs, playfield, action))
            return false;

        HitEffects.Spawn(context.Entities, playfield, noteEntity, note, hitPosition, hitColor, hitRadius, chartIndex);
        if (note.IsDash)
            StartDash(playfield, action, offsetMs);
        else if (note.IsOrb)
            Grab(playfield, note, action);
        return true;
    }

    /// <summary>
    /// The head of an orb hold, taken by a dash that reaches it: the dash is
    /// instant, so the body is on the orb at once, and rides it by song time from
    /// the head (so a late grab is already that far along), held by that direction key.
    /// </summary>
    private static void Grab(Entity playfield, in Note orb, KeybindAction action)
    {
        if (!playfield.HasComponent<CurveBody>())
            return;

        ref CurveBody body = ref playfield.GetComponent<CurveBody>();
        Vector2 from = body.Position;
        body.Dash = Vector2.Zero;
        body.OnOrb = true;
        body.HoldOpen = true;
        body.Ride = orb;
        body.RideKey = action;
        double now = Math.Max(body.SongMs, orb.TimeMs);
        body.Position = Orbs.At(orb, now) + Orbs.Hang;
        body.Velocity = Orbs.Velocity(orb, now);
        // The dash onto it is instant, drawn like any dash: streak, afterimages, shockwave.
        body.DashFrom = from;
        body.DashTo = body.Position;
        body.DashStartMs = body.SongMs;

        // The dash onto it is instant: a dash's spray off where the body left.
        Vector2 way = PlayRules.DashDirection(action);
        if (Vector2.DistanceSquared(body.Position, from) > 1f && playfield.TryGetComponent(out PlayfieldView view))
        {
            playfield.Store.CreateEntity(new ParticleBurst(
                view.PlayfieldToScreen(from),
                GameParticles.Dash,
                PlayfieldColors.NeutralColor,
                PlayRules.BodySize * view.Scale,
                heading: -way));
        }

        PlayCameraSystem.Kick(playfield, 2.6f);
    }

    /// <summary>
    /// Sends the body off the way the key points. A dash is instant: the curve
    /// player puts the body at its end (a wall, a dash ender, a pad) on its next step.
    /// </summary>
    private static void StartDash(Entity playfield, KeybindAction action, long offsetMs)
    {
        Vector2 direction = PlayRules.DashDirection(action);
        if (direction == Vector2.Zero || !playfield.HasComponent<CurveBody>())
            return;

        ref CurveBody body = ref playfield.GetComponent<CurveBody>();
        // A dash throws the body off an orb; its tail is still judged on release.
        body.OnOrb = false;
        body.Dash = direction;
        body.DashFrom = body.Position;
        body.DashStartMs = body.SongMs;
        body.Velocity = direction;

        // A spray thrown back the way the body came from, and a harder kick than a tap's.
        if (playfield.TryGetComponent(out PlayfieldView view))
        {
            playfield.Store.CreateEntity(new ParticleBurst(
                view.PlayfieldToScreen(body.DashFrom),
                GameParticles.Dash,
                PlayfieldColors.NeutralColor,
                PlayRules.BodySize * view.Scale,
                heading: -direction));
        }

        PlayCameraSystem.Kick(playfield, 2.6f);
    }
}

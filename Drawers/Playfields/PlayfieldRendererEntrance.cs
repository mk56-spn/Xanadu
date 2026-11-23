using System;
using Friflo.Engine.ECS;
using Xanadu.Core.Items;
using Xanadu.Core.Maths;
using Xanadu.Model.Animation;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Levels;
using Xanadu.Model.Playfields;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Drawers.Playfields;

/// <summary>
/// The entrance: a rift in the middle of the field the player is born from.
/// Dormant, it is a thin seam of light that breathes on the beat, with motes
/// drifting into it. Over the four beats before its entrance note it charges:
/// the seam opens, a ring implodes onto it on every beat, four pips count the
/// beats in, and the motes rush. On the note's time it releases: a white flare
/// up and down the field, two shockwaves and a ring of sparks, and the rift
/// snaps shut behind the player (whose own pop is in the player drawer).
/// It is drawn as Xifs (<see cref="Rifts"/>): this works out the moment's
/// numbers and sets them on the looks; an orb exit's path is still drawn here.
/// </summary>
public static partial class PlayfieldRenderer
{
    /// <summary>How many beats the rift charges for before it releases.</summary>
    private const int ChargeBeats = 4;

    /// <summary>The longest the release lasts, ms; a beat when beats are shorter.</summary>
    private const double ReleaseMaxMs = 650;

    public static void DrawEntrance(IRectCanvas canvas, Handle rid, EntityStore entities, PlayfieldView view, Playfield playfield, Playhead playhead)
    {
        if (Entrance(entities) is not (double release, Vector2 place, Note entrance))
            return;

        Vector2 centre = view.PlayfieldToScreen(place);
        double now = playhead.ShownMs;
        double beatMs = TempoMap.BeatMs(playhead.Tempo.At(release).Bpm);
        double releaseMs = Math.Min(beatMs, ReleaseMaxMs);
        float halfHeight = Rifts.HalfHeight, bodyHalf = Rifts.BodyHalf;
        if (now >= release + releaseMs)
            return;

        Xif look = Rifts.Look;
        if (now >= release)
        {
            float t = (float)((now - release) / releaseMs);
            DrawReleaseFlare(canvas, rid, centre, t, view, playfield);
            Rifts.Set(look, "rift_release", t, 1f);
            DrawRift(canvas, rid, centre, view);
            return;
        }

        // How far into the charge, 0 until it starts, 1 on the release.
        double chargeStart = release - ChargeBeats * beatMs;
        float charge = (float)Math.Clamp((now - chargeStart) / (release - chargeStart), 0d, 1d);
        double beat = playhead.Tempo.BeatAt(now);
        float since = (float)(beat - Math.Floor(beat));
        float pulse = (1f - since) * (1f - since);
        float open = bodyHalf * (0.08f + 0.55f * charge * charge) * (1f + 0.35f * pulse);
        // One mote trip in takes two beats dormant, half a beat fully charged.
        double tripMs = beatMs * (2d - 1.5d * charge);

        Rifts.Set(look, "rift_release", 0f);
        Rifts.Set(look, "rift_glow", bodyHalf * (2.5f + 3f * charge), halfHeight * (1.3f + 0.6f * charge), 0.18f + 0.35f * charge + 0.12f * pulse);
        Rifts.Set(look, "rift_motes", (float)((now / tripMs) % 1d), halfHeight * (2.6f + 0.8f * charge), 0.35f + 0.6f * charge, 1f);
        Rifts.Set(look, "rift_ring", halfHeight * (0.7f + 2.6f * pulse), 2f + 2f * since,
            charge > 0f ? 0.55f * since * MathF.Min(1f, charge * ChargeBeats) : 0f);
        Rifts.Set(look, "rift_lens", open * 2.2f, open, 0.35f + 0.3f * pulse, 0.85f + 0.15f * pulse);
        double left = (release - now) / beatMs;
        Rifts.Set(look, "rift_pips", (float)left, left <= ChargeBeats ? 1f : 0f, 22f, 6f);
        SetExit(look, entrance, 0.25f + 0.75f * charge);
        DrawRift(canvas, rid, centre, view);
        DrawOrbExit(canvas, rid, entrance, view, 0.25f + 0.75f * charge);
    }

    /// <summary>The rift's look at its place, as its numbers were just set.</summary>
    private static void DrawRift(IRectCanvas canvas, Handle rid, Vector2 centre, PlayfieldView view)
    {
        var placement = new Transform2D(new Vector2(view.Scale, 0f), new Vector2(0f, view.Scale), centre);
        XifDrawer.Shared.DrawNode(canvas, rid, Rifts.Look, placement, view.Scale, XifTint.Plain);
    }

    /// <summary>The release's flare: a white column up and down the field in a blue spread, thinning fast.</summary>
    private static void DrawReleaseFlare(IRectCanvas canvas, Handle rid, Vector2 centre, float t, PlayfieldView view, Playfield playfield)
    {
        float fast = MathF.Max(0f, 1f - t * 2.5f);
        float flareHalf = Rifts.BodyHalf * view.Scale * 1.6f * fast;
        if (flareHalf <= 0.5f)
            return;

        Rect2 field = PlayfieldMetrics.FieldRect(view, playfield);
        Rifts.Set(Rifts.FlareLook, "rift_flare", fast);
        var placement = new Transform2D(new Vector2(flareHalf * 6f, 0f), new Vector2(0f, field.Size.Y), new Vector2(centre.X - flareHalf * 3f, field.Position.Y));
        XifDrawer.Shared.DrawNode(canvas, rid, Rifts.FlareLook, placement, 1f, XifTint.Plain);
    }

    /// <summary>A dash exit's arrow on the look, the way it launches the player; none for other exits.</summary>
    private static void SetExit(Xif look, in Note entrance, float alpha)
    {
        Vector2 way = entrance.Exit == EntranceExit.Dash ? PlayRules.DashDirection(entrance.Target) : Vector2.Zero;
        Rifts.Set(look, "rift_exit", way.X, way.Y, alpha, way == Vector2.Zero ? 0f : 1f);
    }

    /// <summary>An orb exit's path ahead of it, and the ring where it ends.</summary>
    private static void DrawOrbExit(IRectCanvas canvas, Handle rid, in Note entrance, PlayfieldView view, float alpha)
    {
        if (entrance.Exit != EntranceExit.Orb || entrance.EndMs <= entrance.TimeMs)
            return;

        Note orb = Orbs.OfEntrance(entrance);
        DrawOrbPath(canvas, rid, orb, view, orb.TimeMs, Model.Playfields.PlayfieldColors.OrbColor with { A = 0.5f * alpha });
        canvas.DrawCircleOutline(rid, view.PlayfieldToScreen(Orbs.At(orb, orb.EndMs)), Orbs.Radius * view.Scale * 0.8f,
            Model.Playfields.PlayfieldColors.OrbColor with { A = alpha }, 2f);
    }

    /// <summary>
    /// When and where the player leaves the entrance: pinned on the body in play
    /// (the entrance note is gone once hit), else the earliest entrance note.
    /// Null for a level without one.
    /// </summary>
    private static (double ReleaseMs, Vector2 At, Note Entrance)? Entrance(EntityStore entities)
    {
        foreach (var (bodies, _) in entities.Query<CurveBody>().Chunks)
        {
            foreach (CurveBody body in bodies.Span)
            {
                if (body.Placed)
                    return double.IsNaN(body.ReleaseMs) ? null : (body.ReleaseMs, body.EntranceAt, body.Entrance);
            }
        }

        Note? first = null;
        foreach (var (notes, _) in entities.Query<Note>().Chunks)
        {
            foreach (Note note in notes.Span)
            {
                if (note.IsEntrance && (first is not { } earliest || note.TimeMs < earliest.TimeMs))
                    first = note;
            }
        }

        return first is { } entrance ? (entrance.TimeMs, entrance.Position, entrance) : null;
    }

    /// <summary>
    /// An entrance note in the editor: the rift's seam at its place, bright
    /// over the four beats it charges for and the beat it releases on, faint
    /// otherwise, with a selection ring when picked.
    /// </summary>
    public static void DrawEntranceMarker(IRectCanvas canvas, Handle rid, in Note note, PlayfieldView view, Playhead playhead, bool selected)
    {
        Vector2 centre = view.PlayfieldToScreen(note.Position);
        double beatMs = TempoMap.BeatMs(playhead.Tempo.At(note.TimeMs).Bpm);
        double now = playhead.ShownMs;
        bool near = now >= note.TimeMs - ChargeBeats * beatMs && now <= note.TimeMs + beatMs;
        float alpha = near ? 1f : 0.4f;

        Xif look = Rifts.Look;
        Rifts.Set(look, "rift_release", 0f);
        Rifts.Set(look, "rift_glow", Rifts.BodyHalf * 3f, Rifts.HalfHeight * 1.4f, 0.3f * alpha);
        Rifts.Set(look, "rift_motes", 0f);
        Rifts.Set(look, "rift_ring", 0f, 2f);
        Rifts.Set(look, "rift_lens", 0f, Rifts.BodyHalf * 0.5f, 0f, 0.9f * alpha);
        Rifts.Set(look, "rift_pips", ChargeBeats, 0f, 22f, 6f);
        SetExit(look, note, alpha);
        DrawRift(canvas, rid, centre, view);
        DrawOrbExit(canvas, rid, note, view, alpha);

        foreach (OrbKey key in note.Exit == EntranceExit.Orb ? note.Path ?? [] : [])
            canvas.DrawCircle(rid, view.PlayfieldToScreen(Orbs.At(Orbs.OfEntrance(note), note.TimeMs + key.OffsetMs)), MathF.Max(3f, Orbs.Radius * view.Scale * 0.3f),
                Model.Playfields.PlayfieldColors.OrbColor with { A = alpha });
        if (selected)
            canvas.DrawCircleOutline(rid, centre, Rifts.HalfHeight * view.Scale * 1.15f, Model.Playfields.PlayfieldColors.SelectedNoteColor, 2f);
    }
}

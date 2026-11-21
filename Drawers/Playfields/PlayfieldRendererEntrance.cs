using System;
using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
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
/// </summary>
public static partial class PlayfieldRenderer
{
    /// <summary>How many beats the rift charges for before it releases.</summary>
    private const int ChargeBeats = 4;

    /// <summary>The longest the release lasts, ms; a beat when beats are shorter.</summary>
    private const double ReleaseMaxMs = 650;

    private const int Motes = 18;

    private const int Sparks = 12;

    private static readonly Color RiftBlue = new(0.4f, 0.65f, 1f, 1f);

    public static void DrawEntrance(IRectCanvas canvas, Handle rid, EntityStore entities, PlayfieldView view, Playfield playfield, Playhead playhead)
    {
        if (Entrance(entities) is not (double release, Vector2 place, Note entrance))
            return;

        float scale = view.Scale;
        Vector2 centre = view.PlayfieldToScreen(place);
        double now = playhead.ShownMs;
        double beatMs = TempoMap.BeatMs(playhead.Tempo.At(release).Bpm);
        double releaseMs = Math.Min(beatMs, ReleaseMaxMs);
        float halfHeight = PlayRules.BodyHeight * 0.8f * scale;
        float bodyHalf = PlayRules.BodyWidth / 2f * scale;

        if (now >= release + releaseMs)
            return;

        if (now >= release)
        {
            DrawRelease(canvas, rid, centre, (float)((now - release) / releaseMs), halfHeight, bodyHalf, view, playfield);
            return;
        }

        // How far into the charge, 0 until it starts, 1 on the release.
        double chargeStart = release - ChargeBeats * beatMs;
        float charge = (float)Math.Clamp((now - chargeStart) / (release - chargeStart), 0d, 1d);
        double beat = playhead.Tempo.BeatAt(now);
        float since = (float)(beat - Math.Floor(beat));
        float pulse = (1f - since) * (1f - since);

        // The glow round it, growing as it charges.
        float glow = 0.18f + 0.35f * charge + 0.12f * pulse;
        DrawSoftEllipse(canvas, rid, centre, bodyHalf * (2.5f + 3f * charge), halfHeight * (1.3f + 0.6f * charge), RiftBlue with { A = glow });

        DrawMotes(canvas, rid, centre, halfHeight, now, beatMs, charge);

        // A ring imploding onto the rift on every beat of the charge.
        if (charge > 0f)
        {
            float ring = halfHeight * (0.7f + 2.6f * (1f - since) * (1f - since));
            canvas.DrawCircleOutline(rid, centre, ring, new Color(1f, 1f, 1f, 0.55f * since * MathF.Min(1f, charge * ChargeBeats)), 2f + 2f * since);
        }

        // The seam: a sliver of light, opening as it charges and kicking on the beat.
        float open = bodyHalf * (0.08f + 0.55f * charge * charge) * (1f + 0.35f * pulse);
        DrawLens(canvas, rid, centre, open * 2.2f, halfHeight * 1.05f, RiftBlue with { A = 0.35f + 0.3f * pulse });
        DrawLens(canvas, rid, centre, open, halfHeight, new Color(0.9f, 0.96f, 1f, 0.85f + 0.15f * pulse));

        DrawCountPips(canvas, rid, centre + new Vector2(0f, halfHeight + 26f * scale), scale, now, release, beatMs);
        DrawExitHint(canvas, rid, entrance, centre, halfHeight, view, 0.25f + 0.75f * charge);
    }

    /// <summary>The release: a flare the height of the field, two shockwaves, sparks flying out, and the rift snapping shut.</summary>
    private static void DrawRelease(IRectCanvas canvas, Handle rid, Vector2 centre, float t, float halfHeight, float bodyHalf, PlayfieldView view, Playfield playfield)
    {
        float fade = 1f - t;
        float fast = MathF.Max(0f, 1f - t * 2.5f);

        // The flare: a white column up and down the field, thinning fast.
        Rect2 field = PlayfieldMetrics.FieldRect(view, playfield);
        float flareHalf = bodyHalf * 1.6f * fast;
        if (flareHalf > 0.5f)
        {
            canvas.DrawGradientRect(rid, new Rect2(centre.X - flareHalf * 3f, field.Position.Y, flareHalf * 3f, field.Size.Y), RiftBlue with { A = 0f }, RiftBlue with { A = 0.5f * fast }, horizontal: true);
            canvas.DrawGradientRect(rid, new Rect2(centre.X, field.Position.Y, flareHalf * 3f, field.Size.Y), RiftBlue with { A = 0.5f * fast }, RiftBlue with { A = 0f }, horizontal: true);
            canvas.DrawRect(rid, new Rect2(centre.X - flareHalf / 2f, field.Position.Y, flareHalf, field.Size.Y), new Color(1f, 1f, 1f, 0.9f * fast));
        }

        // A quick bloom of light, gone well before the shockwaves are.
        if (fast > 0f)
            DrawSoftEllipse(canvas, rid, centre, halfHeight * (1f + 1.2f * t), halfHeight * (1f + 1.2f * t), new Color(0.85f, 0.92f, 1f, 0.55f * fast * fast));

        // Two shockwaves, the second a little behind.
        float wave = 1f - (1f - t) * (1f - t);
        canvas.DrawCircleOutline(rid, centre, halfHeight * (0.6f + 4f * wave), new Color(1f, 1f, 1f, 0.9f * fade), 2f + 8f * fade);
        float late = Math.Clamp((t - 0.15f) / 0.85f, 0f, 1f);
        if (late > 0f)
        {
            float lateWave = 1f - (1f - late) * (1f - late);
            canvas.DrawCircleOutline(rid, centre, halfHeight * (0.5f + 2.6f * lateWave), RiftBlue with { A = 0.8f * (1f - late) }, 2f + 4f * (1f - late));
        }

        // Sparks: short streaks racing out all round.
        for (int i = 0; i < Sparks; i++)
        {
            float angle = i * MathF.Tau / Sparks + 0.3f * MathF.Sin(i * 2.1f);
            Vector2 way = new(MathF.Cos(angle), MathF.Sin(angle));
            float reach = halfHeight * (0.5f + (2.8f + 0.8f * MathF.Sin(i * 1.7f)) * wave);
            float length = halfHeight * 0.45f * fade;
            canvas.DrawLine(rid, centre + way * reach, centre + way * (reach + length), new Color(1f, 1f, 1f, fade), 2f + 2f * fade);
        }

        // The rift flung wide as the player comes through, then snapping shut.
        float shut = Math.Clamp(t / 0.35f, 0f, 1f);
        float open = bodyHalf * 1.3f * (1f - shut * shut);
        if (open > 0.3f)
            DrawLens(canvas, rid, centre, open, halfHeight * (1f + 0.2f * (1f - shut)), new Color(1f, 1f, 1f, 0.95f * (1f - shut)));
    }

    /// <summary>Motes spiralling into the rift, faster as it charges.</summary>
    private static void DrawMotes(IRectCanvas canvas, Handle rid, Vector2 centre, float halfHeight, double now, double beatMs, float charge)
    {
        // One trip in takes two beats dormant, half a beat fully charged.
        double tripMs = beatMs * (2d - 1.5d * charge);
        float reach = halfHeight * (2.6f + 0.8f * charge);
        for (int i = 0; i < Motes; i++)
        {
            double phase = now / tripMs + i / (double)Motes;
            float p = (float)(phase - Math.Floor(phase));
            float r = reach * (1f - p) * (1f - p) + halfHeight * 0.08f;
            float angle = i * 2.39996f + p * 1.6f;
            Vector2 at = centre + new Vector2(MathF.Cos(angle) * r * 0.75f, MathF.Sin(angle) * r);
            float alpha = 4f * p * (1f - p) * (0.35f + 0.6f * charge);
            canvas.DrawCircle(rid, at, 1.5f + 2f * p, new Color(0.75f, 0.88f, 1f, alpha));
        }
    }

    /// <summary>Four pips under the rift, one lighting per beat of the charge, so the release can be counted in.</summary>
    private static void DrawCountPips(IRectCanvas canvas, Handle rid, Vector2 at, float scale, double now, double release, double beatMs)
    {
        double left = (release - now) / beatMs;
        if (left > ChargeBeats)
            return;

        float gap = 22f * scale;
        float radius = MathF.Max(2.5f, 6f * scale);
        for (int i = 0; i < ChargeBeats; i++)
        {
            Vector2 pip = at + new Vector2((i - (ChargeBeats - 1) / 2f) * gap, 0f);
            // Pip i lights when that many beats of the charge have gone.
            double litFor = (ChargeBeats - left) - i;
            if (litFor >= 0)
            {
                float flash = (float)Math.Max(0d, 1d - litFor * 2d);
                canvas.DrawCircle(rid, pip, radius * (1f + 0.6f * flash), new Color(1f, 1f, 1f, 0.95f));
            }
            else
                canvas.DrawCircleOutline(rid, pip, radius, RiftBlue with { A = 0.6f }, 1.5f);
        }
    }

    /// <summary>A vertical lens, pointed top and bottom: the rift's shape.</summary>
    private static void DrawLens(IRectCanvas canvas, Handle rid, Vector2 centre, float halfWidth, float halfHeight, Color color)
    {
        if (halfWidth < 0.3f)
        {
            canvas.DrawLine(rid, centre - new Vector2(0f, halfHeight), centre + new Vector2(0f, halfHeight), color, 1f);
            return;
        }

        const int Steps = 16;
        Span<Vector2> points = stackalloc Vector2[Steps * 2];
        for (int i = 0; i < Steps; i++)
        {
            float u = i / (float)(Steps - 1);
            float y = -halfHeight + 2f * halfHeight * u;
            float x = halfWidth * MathF.Pow(MathF.Sin(MathF.PI * u), 0.7f);
            points[i] = centre + new Vector2(x, y);
            points[Steps * 2 - 1 - i] = centre + new Vector2(-x, y);
        }

        canvas.DrawPolygon(rid, points, color);
    }

    /// <summary>A soft glow: an ellipse fading from <paramref name="color"/> at its middle to nothing at its edge.</summary>
    private static void DrawSoftEllipse(IRectCanvas canvas, Handle rid, Vector2 centre, float radiusX, float radiusY, Color color)
    {
        const int Steps = 32;
        Span<Vector2> points = stackalloc Vector2[Steps * 3];
        Span<Color> colors = stackalloc Color[Steps * 3];
        Color edge = color with { A = 0f };
        for (int i = 0; i < Steps; i++)
        {
            float a0 = i * MathF.Tau / Steps;
            float a1 = (i + 1) * MathF.Tau / Steps;
            points[i * 3] = centre;
            points[i * 3 + 1] = centre + new Vector2(MathF.Cos(a0) * radiusX, MathF.Sin(a0) * radiusY);
            points[i * 3 + 2] = centre + new Vector2(MathF.Cos(a1) * radiusX, MathF.Sin(a1) * radiusY);
            colors[i * 3] = color;
            colors[i * 3 + 1] = edge;
            colors[i * 3 + 2] = edge;
        }

        canvas.DrawTriangles(rid, points, colors);
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
    /// What the rift will launch the player into, shown ahead: an arrow the
    /// way a dash exit goes, or the path of an orb exit. A fall shows nothing.
    /// </summary>
    private static void DrawExitHint(IRectCanvas canvas, Handle rid, in Note entrance, Vector2 centre, float halfHeight, PlayfieldView view, float alpha)
    {
        switch (entrance.Exit)
        {
            case EntranceExit.Dash when PlayRules.DashDirection(entrance.Target) is var way && way != Vector2.Zero:
            {
                Vector2 from = centre + way * halfHeight * 0.7f;
                Vector2 tip = centre + way * halfHeight * 1.9f;
                Vector2 side = new Vector2(-way.Y, way.X) * halfHeight * 0.25f;
                Color colour = new(1f, 1f, 1f, 0.85f * alpha);
                canvas.DrawLine(rid, from, tip, colour, 3f);
                canvas.DrawLine(rid, tip, tip - way * halfHeight * 0.3f + side, colour, 3f);
                canvas.DrawLine(rid, tip, tip - way * halfHeight * 0.3f - side, colour, 3f);
                break;
            }
            case EntranceExit.Orb when entrance.EndMs > entrance.TimeMs:
            {
                Note orb = Orbs.OfEntrance(entrance);
                DrawOrbPath(canvas, rid, orb, view, orb.TimeMs, Model.Playfields.PlayfieldColors.OrbColor with { A = 0.5f * alpha });
                canvas.DrawCircleOutline(rid, view.PlayfieldToScreen(Orbs.At(orb, orb.EndMs)), Orbs.Radius * view.Scale * 0.8f,
                    Model.Playfields.PlayfieldColors.OrbColor with { A = alpha }, 2f);
                break;
            }
        }
    }

    /// <summary>
    /// An entrance note in the editor: the rift's seam at its place, bright
    /// over the four beats it charges for and the beat it releases on, faint
    /// otherwise, with a selection ring when picked.
    /// </summary>
    public static void DrawEntranceMarker(IRectCanvas canvas, Handle rid, in Note note, PlayfieldView view, Playhead playhead, bool selected)
    {
        Vector2 centre = view.PlayfieldToScreen(note.Position);
        float halfHeight = PlayRules.BodyHeight * 0.8f * view.Scale;
        float bodyHalf = PlayRules.BodyWidth / 2f * view.Scale;
        double beatMs = TempoMap.BeatMs(playhead.Tempo.At(note.TimeMs).Bpm);
        double now = playhead.ShownMs;
        bool near = now >= note.TimeMs - ChargeBeats * beatMs && now <= note.TimeMs + beatMs;
        float alpha = near ? 1f : 0.4f;
        DrawSoftEllipse(canvas, rid, centre, bodyHalf * 3f, halfHeight * 1.4f, RiftBlue with { A = 0.3f * alpha });
        DrawLens(canvas, rid, centre, bodyHalf * 0.5f, halfHeight, new Color(0.9f, 0.96f, 1f, 0.9f * alpha));
        DrawExitHint(canvas, rid, note, centre, halfHeight, view, alpha);
        foreach (OrbKey key in note.Exit == EntranceExit.Orb ? note.Path ?? [] : [])
            canvas.DrawCircle(rid, view.PlayfieldToScreen(Orbs.At(Orbs.OfEntrance(note), note.TimeMs + key.OffsetMs)), MathF.Max(3f, Orbs.Radius * view.Scale * 0.3f),
                Model.Playfields.PlayfieldColors.OrbColor with { A = alpha });
        if (selected)
            canvas.DrawCircleOutline(rid, centre, halfHeight * 1.15f, Model.Playfields.PlayfieldColors.SelectedNoteColor, 2f);
    }
}

using System;
using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Model.Animation;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Playfields;
using Xanadu.Ui.Systems.Canvas;
using static Xanadu.Model.Playfields.PlayfieldColors;

namespace Xanadu.Drawers.Playfields;

/// <summary>
/// Orb holds (<see cref="Orbs"/>). In play an orb shows from
/// <see cref="Orbs.ApproachMs"/> before its head: waiting at its start with a
/// ring closing on it, its path ahead drawn to the tail. Once grabbed its note
/// is gone and the body's copy (<see cref="CurveBody.Ride"/>) is drawn instead.
/// A missed orb goes on along its path, greyed. In the editor every orb shows
/// its whole path, its keys and where it is at the playhead.
/// </summary>
public static partial class PlayfieldRenderer
{
    private const double PathStepMs = 25;

    /// <summary>An orb still waiting for its head (or missed), or any orb in the editor.</summary>
    public static void DrawOrbNote(IRectCanvas canvas, Handle rid, Entity entity, in PlayfieldItemFrame frame)
    {
        Note orb = entity.GetComponent<Note>();
        double now = frame.Playhead.ShownMs;
        bool selected = !frame.SelectedNote.IsNull && frame.SelectedNote.Id == entity.Id;
        if (frame.Editor)
        {
            DrawOrbEditor(canvas, rid, orb, frame.View, now, selected);
            return;
        }

        bool missed = entity.Tags.Has<NoteMissed>();
        float alpha = OrbAlpha(orb, now);
        if (alpha <= 0f)
            return;

        Color colour = missed ? new Color(0.45f, 0.45f, 0.5f, 1f) : OrbColor;
        DrawOrbPath(canvas, rid, orb, frame.View, Math.Max(now, orb.TimeMs), colour with { A = 0.45f * alpha });
        Vector2 at = frame.View.PlayfieldToScreen(Orbs.At(orb, now));
        float approach = !missed && now < orb.TimeMs ? (float)((orb.TimeMs - now) / Orbs.ApproachMs) : -1f;
        DrawOrbBall(canvas, rid, at, frame.View.Scale, colour, alpha, lit: false, approach);
    }

    /// <summary>The orb the body grabbed, from its copy on the body, until a little after its tail.</summary>
    public static void DrawRiddenOrb(IRectCanvas canvas, Handle rid, EntityStore entities, PlayfieldView view)
    {
        foreach (var (bodies, _) in entities.Query<CurveBody>().Chunks)
        {
            foreach (CurveBody body in bodies.Span)
            {
                Note orb = body.Ride;
                if (!orb.IsOrb)
                    continue;

                double now = body.SongMs;
                float alpha = OrbAlpha(orb, now);
                if (alpha <= 0f)
                    continue;

                DrawOrbPath(canvas, rid, orb, view, now, OrbColor with { A = 0.6f * alpha });
                Vector2 at = view.PlayfieldToScreen(Orbs.At(orb, now));
                DrawOrbBall(canvas, rid, at, view.Scale * (body.OnOrb ? 1.25f : 1f), OrbColor, alpha, lit: body.OnOrb);
            }
        }
    }

    /// <summary>In the editor: the whole path, a dot on each key, the tail ring, and the orb where it is at the playhead.</summary>
    private static void DrawOrbEditor(IRectCanvas canvas, Handle rid, in Note orb, PlayfieldView view, double now, bool selected)
    {
        bool live = now >= orb.TimeMs - Orbs.ApproachMs && now <= orb.EndMs + Orbs.LingerMs;
        float alpha = live ? 1f : 0.45f;
        DrawOrbPath(canvas, rid, orb, view, orb.TimeMs, OrbColor with { A = 0.5f * alpha });
        float radius = Orbs.Radius * view.Scale;
        foreach (OrbKey key in orb.Path ?? [])
            canvas.DrawCircle(rid, view.PlayfieldToScreen(Orbs.At(orb, orb.TimeMs + key.OffsetMs)), MathF.Max(3f, radius * 0.3f), OrbColor with { A = alpha });
        canvas.DrawCircleOutline(rid, view.PlayfieldToScreen(Orbs.At(orb, orb.EndMs)), radius * 0.8f, OrbColor with { A = alpha }, 2f);

        Vector2 start = view.PlayfieldToScreen(orb.Position);
        DrawOrbBall(canvas, rid, start, view.Scale, OrbColor, alpha * 0.6f, lit: false);
        if (live)
            DrawOrbBall(canvas, rid, view.PlayfieldToScreen(Orbs.At(orb, now)), view.Scale, OrbColor, 1f, lit: false);
        if (selected)
            canvas.DrawCircleOutline(rid, start, radius * 1.6f, SelectedNoteColor, 2f);
    }

    /// <summary>Where a click of the orb tool would place or key an orb, faint (red when it can't).</summary>
    public static void DrawOrbGhost(IRectCanvas canvas, Handle rid, Vector2 at, float radius, bool valid) =>
        DrawOrbBall(canvas, rid, at, radius / Orbs.Radius, valid ? OrbColor : Xanadu.Core.Theme.Palette.Danger, 0.45f, lit: false);

    /// <summary>The path from <paramref name="fromMs"/> to the tail, as a line.</summary>
    private static void DrawOrbPath(IRectCanvas canvas, Handle rid, in Note orb, PlayfieldView view, double fromMs, Color colour)
    {
        if (fromMs >= orb.EndMs)
            return;

        int steps = Math.Clamp((int)((orb.EndMs - fromMs) / PathStepMs) + 1, 1, 400);
        Span<Vector2> points = stackalloc Vector2[steps + 1];
        for (int i = 0; i <= steps; i++)
            points[i] = view.PlayfieldToScreen(Orbs.At(orb, fromMs + (orb.EndMs - fromMs) * i / steps));
        canvas.DrawPolyline(rid, points, colour, MathF.Max(2f, 4f * view.Scale));
    }

    /// <summary>
    /// The orb's ball (<see cref="Orbs.Look"/>: a soft glow round a bright core,
    /// brighter while ridden, with its approach ring) drawn by the Xif drawer at
    /// <paramref name="scale"/> screen pixels a unit, its colour and state set first.
    /// </summary>
    private static void DrawOrbBall(IRectCanvas canvas, Handle rid, Vector2 at, float scale, Color colour, float alpha, bool lit, float approach = -1f)
    {
        Orbs.SetState(colour, alpha, lit, approach);
        var placement = new Transform2D(new Vector2(scale, 0f), new Vector2(0f, scale), at);
        XifDrawer.Shared.DrawNode(canvas, rid, Orbs.Look, placement, scale, XifTint.Plain);
    }

    /// <summary>In over the approach, full through the hold, out over <see cref="Orbs.LingerMs"/> after the tail.</summary>
    private static float OrbAlpha(in Note orb, double now)
    {
        if (now < orb.TimeMs - Orbs.ApproachMs || now > orb.EndMs + Orbs.LingerMs)
            return 0f;
        if (now < orb.TimeMs)
            return (float)Math.Clamp((now - (orb.TimeMs - Orbs.ApproachMs)) / 150d, 0d, 1d);
        if (now > orb.EndMs)
            return (float)(1d - (now - orb.EndMs) / Orbs.LingerMs);
        return 1f;
    }
}

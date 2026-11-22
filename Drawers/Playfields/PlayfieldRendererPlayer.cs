using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Playfields;
using Xanadu.Ui.Systems.Canvas;
using Position = Xanadu.Core.Position;
using static Xanadu.Model.Playfields.PlayfieldColors;

namespace Xanadu.Drawers.Playfields;

public static partial class PlayfieldRenderer
{
    /// <summary>The player: the hovering blue body, 1 wide by 2.5 tall, where the curve player put it and bobbing slightly (<see cref="PlayRules.Bob"/>).</summary>
    public static void DrawPlayer(
        IRectCanvas canvas,
        Handle rid,
        EntityStore entities,
        PlayfieldView view)
    {
        // One player at most.
        foreach (Entity entity in entities.Query<Player, Position>().Entities)
        {
            Vector2 center = entity.GetComponent<Position>().Value;
            if (entity.TryGetComponent(out PlayCamera camera))
                center += camera.Subpixel;

            Vector2 half = PlayRules.BodySize * view.Scale / 2;
            bool preview = entity.TryGetComponent(out BodyTrace trace);
            if (preview)
                DrawBodyTrace(canvas, rid, trace.Points, view, center);

            bool moved = entity.TryGetComponent(out CurveBody curveBody);
            float alpha = moved ? curveBody.Alpha : 1f;
            if (alpha <= 0f)
                continue;

            // The note hitbox, only in an editor preview: the circle through the body's corners, round the judged centre.
            if (preview)
                canvas.DrawCircleOutline(rid, center, NoteReach.OnScreen(view.Scale), Faded(BodyEdge, alpha * 0.35f), 1.5f);
            if (moved)
                center.Y += PlayRules.Bob(curveBody.SongMs) * view.Scale;

            // The body never squashes or stretches: a hit and coming out of the rift only flash it white.
            float punch = moved && entity.TryGetComponent(out BodyPunch hit) ? hit.At(curveBody.SongMs) : 0f;
            if (moved)
            {
                DrawDashSmear(canvas, rid, curveBody, view, center - entity.GetComponent<Position>().Value, half, alpha);
                punch = MathF.Max(punch, BornFlash(curveBody));
            }

            // The bone puppet stands in for the plain body when there is one, flashed the same way.
            if (entity.TryGetComponent(out Puppet puppet) && puppet.Package is not null)
            {
                // The body box behind the puppet, faint, to check the puppet against it.
                canvas.DrawRect(rid, new Rect2(center - half, half * 2f), Faded(BodyFill, alpha * 0.25f));
                DrawPuppet(canvas, rid, puppet, center, view.Scale, alpha, punch);
                continue;
            }

            Span<Vector2> corners = stackalloc Vector2[5];
            corners[0] = center + new Vector2(-half.X, -half.Y);
            corners[1] = center + new Vector2(half.X, -half.Y);
            corners[2] = center + new Vector2(half.X, half.Y);
            corners[3] = center + new Vector2(-half.X, half.Y);
            corners[4] = corners[0];
            canvas.DrawPolygon(rid, corners[..4], Faded(BodyFill, alpha));
            canvas.DrawPolyline(rid, corners, Faded(BodyEdge, alpha), 2);
            if (punch > 0f)
                canvas.DrawPolygon(rid, corners[..4], new Color(1f, 1f, 1f, 0.7f * punch * alpha));

            static Color Faded(Color color, float alpha) => new(color.R, color.G, color.B, color.A * alpha);
        }
    }

    /// <summary>How long the body flashes white coming out of the rift, ms.</summary>
    private const double BornMs = 260;

    /// <summary>How white the body still is from coming out of the rift, 0 to 1.</summary>
    private static float BornFlash(in CurveBody body)
    {
        if (double.IsNaN(body.ReleaseMs))
            return 0f;

        double age = body.SongMs - body.ReleaseMs;
        if (age < 0 || age >= BornMs)
            return 0f;

        float t = (float)(age / BornMs);
        return (1f - t) * (1f - t);
    }

    /// <summary>How many afterimages an instant dash leaves along its path, and how long they take to fade, ms.</summary>
    private const int Afterimages = 7;
    private const double SmearMs = 180;

    /// <summary>How long a dash's streak burns along its path, and its shockwave spreads from where it landed, ms.</summary>
    private const double StreakMs = 220;
    private const double ShockMs = 260;

    /// <summary>
    /// An instant dash, drawn so it hits: a body-wide streak of light along the
    /// whole path, brightest where it landed with a white-hot core, snapping thin
    /// as it burns out; the afterimages strung along it; and where it landed a
    /// white flash and a shockwave ring. <paramref name="shift"/> is the body's
    /// drawn offset (bob, subpixel).
    /// </summary>
    private static void DrawDashSmear(IRectCanvas canvas, Handle rid, in CurveBody body, PlayfieldView view, Vector2 shift, Vector2 half, float alpha)
    {
        double age = body.SongMs - body.DashStartMs;
        Vector2 path = body.DashTo - body.DashFrom;
        if (age < 0 || age >= Math.Max(ShockMs, Math.Max(StreakMs, SmearMs)) || path.LengthSquared() < 1f)
            return;

        Vector2 way = Vector2.Normalize(path);
        Vector2 from = view.PlayfieldToScreen(body.DashFrom) + shift;
        Vector2 to = view.PlayfieldToScreen(body.DashTo) + shift;
        bool sideways = MathF.Abs(way.X) > MathF.Abs(way.Y);
        float across = (sideways ? half.Y : half.X) * 2f;

        if (age < StreakMs)
        {
            float k = 1f - (float)(age / StreakMs);
            float burn = k * k;
            // The glow, the beam, the core: each thinner and brighter, all thinning as it burns out.
            Streak(canvas, rid, from, to, sideways, across * (0.5f + 0.9f * k), BodyFill with { A = 0.55f * burn * alpha });
            Streak(canvas, rid, from, to, sideways, across * 0.55f * k, new Color(0.75f, 0.9f, 1f, 0.85f * burn * alpha));
            Streak(canvas, rid, from, to, sideways, MathF.Max(2f, across * 0.18f * k), new Color(1f, 1f, 1f, burn * alpha));
        }

        if (age < SmearMs)
        {
            float left = 1f - (float)(age / SmearMs);
            Vector2 shape = half;
            for (int i = 0; i < Afterimages; i++)
            {
                float along = i / (float)Afterimages;
                Vector2 at = Vector2.Lerp(from, to, along);
                float fade = left * (0.4f + 0.6f * along);
                var rect = new Rect2(at - shape, shape * 2f);
                canvas.DrawRect(rid, rect, new Color(BodyFill.R, BodyFill.G, BodyFill.B, 0.7f * fade * alpha));
                canvas.DrawBorder(rid, rect, new Color(1f, 1f, 1f, 0.8f * fade * alpha), 2f);
            }
        }

        if (age < ShockMs)
        {
            float t = (float)(age / ShockMs);
            float k = 1f - t;
            float reach = half.Y * (0.6f + 2.6f * (1f - k * k * k));
            canvas.DrawCircleOutline(rid, to, reach, new Color(1f, 1f, 1f, 0.9f * k * alpha), MathF.Max(2f, 10f * k));
            canvas.DrawCircleOutline(rid, to, reach * 0.7f, BodyFill with { A = 0.6f * k * alpha }, MathF.Max(1.5f, 6f * k));
            // The landing flash: a white burst over the body that dies fast.
            float flash = MathF.Max(0f, 1f - t * 3f);
            if (flash > 0f)
            {
                var burst = new Rect2(to - half * (1.4f - 0.3f * flash), half * 2f * (1.4f - 0.3f * flash));
                canvas.DrawRect(rid, burst, new Color(1f, 1f, 1f, 0.75f * flash * alpha));
            }
        }
    }

    /// <summary>A band of light from <paramref name="from"/> to <paramref name="to"/>, <paramref name="width"/> across, along an axis.</summary>
    private static void Streak(IRectCanvas canvas, Handle rid, Vector2 from, Vector2 to, bool sideways, float width, Color colour)
    {
        Vector2 low = Vector2.Min(from, to), high = Vector2.Max(from, to);
        var band = sideways
            ? new Rect2(low.X, from.Y - width / 2f, high.X - low.X, width)
            : new Rect2(from.X - width / 2f, low.Y, width, high.Y - low.Y);
        // Faint where it set off, full where it landed.
        Color faint = colour with { A = colour.A * 0.25f };
        bool forward = sideways ? to.X >= from.X : to.Y >= from.Y;
        canvas.DrawGradientRect(rid, band, forward ? faint : colour, forward ? colour : faint, horizontal: sideways);
    }

    /// <summary>
    /// The line the body's centre has run along: in an editor preview up to where it is now
    /// (<paramref name="center"/>, shifted like the body), and back in the editor the last preview's whole line.
    /// </summary>
    public static void DrawBodyTrace(IRectCanvas canvas, Handle rid, List<Vector2>? points, PlayfieldView view, Vector2? center = null)
    {
        if (points is null || points.Count + (center is null ? 0 : 1) < 2)
            return;

        var screen = new Vector2[points.Count + (center is null ? 0 : 1)];
        for (int i = 0; i < points.Count; i++)
            screen[i] = view.PlayfieldToScreen(points[i]);
        if (center is Vector2 end)
            screen[^1] = end;
        Color edge = BodyEdge;
        canvas.DrawPolyline(rid, screen, new Color(edge.R, edge.G, edge.B, edge.A * 0.6f), 2);
    }
}

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
    /// <summary>How much wider a hit's punch makes the body at its peak, as a share of its width.</summary>
    private const float PunchWiden = 0.3f;

    /// <summary>How much shorter a hit's punch makes the body at its peak, as a share of its height.</summary>
    private const float PunchFlatten = 0.12f;

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

            // A hit squashes the body wide and short and flashes it white, easing back;
            // a dash shapes it instead, so the punch keeps only its flash then.
            float punch = moved && entity.TryGetComponent(out BodyPunch hit) ? hit.At(curveBody.SongMs) : 0f;
            if (moved && (curveBody.Dashing || DashShape(curveBody) != Vector2.One))
            {
                if (curveBody.Dashing)
                    DrawAfterimages(canvas, rid, curveBody, view, center, half, alpha);
                half *= DashShape(curveBody);
                center += ImpactShift(curveBody) * view.Scale;
            }
            else
                half *= new Vector2(1f + PunchWiden * punch, 1f - PunchFlatten * punch);

            // Coming out of the rift: squeezed thin and tall, springing out wide past round and settling, flashing white.
            float bornFlash = 0f;
            float born = moved ? BornShape(curveBody, out bornFlash) : 0f;
            if (born > 0f)
            {
                float overshoot = MathF.Sin(born * MathF.PI) * 0.3f * (1f - born);
                float open = 1f - (1f - born) * (1f - born) * (1f - born);
                half *= new Vector2(0.12f + 0.88f * open + overshoot, 1.35f - 0.35f * open - overshoot * 0.6f);
                punch = MathF.Max(punch, bornFlash);
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

    /// <summary>How long the body takes to spring out of the rift to its own shape, ms.</summary>
    private const double BornMs = 260;

    /// <summary>
    /// How far the body is through springing out of the rift, 0 to 1, or 0 when
    /// it isn't; <paramref name="flash"/> is how white it still is.
    /// </summary>
    private static float BornShape(in CurveBody body, out float flash)
    {
        flash = 0f;
        if (double.IsNaN(body.ReleaseMs))
            return 0f;

        double age = body.SongMs - body.ReleaseMs;
        if (age < 0 || age >= BornMs)
            return 0f;

        float t = (float)(age / BornMs);
        flash = (1f - t) * (1f - t);
        return MathF.Max(t, 0.001f);
    }

    /// <summary>How long the body takes to ease into its dash stretch, ms.</summary>
    private const double StretchInMs = 60;

    /// <summary>How long the body stays squashed against the wall a dash hit, ms.</summary>
    private const double ImpactMs = 170;

    /// <summary>How many afterimages a dash leaves, and how far apart, as song time behind the body, ms.</summary>
    private const int Afterimages = 5;
    private const float AfterimageGapMs = 22f;

    /// <summary>
    /// How a dash shapes the body, as a scale on its half size: stretched long
    /// along the dash and thin across it while dashing (easing in), then
    /// squashed flat against the wall it hit, springing back.
    /// </summary>
    private static Vector2 DashShape(in CurveBody body)
    {
        if (body.Dashing)
        {
            float into = (float)Math.Clamp((body.SongMs - body.DashStartMs) / StretchInMs, 0d, 1d);
            return Along(body.Dash, 1f + 0.55f * into, 1f - 0.3f * into);
        }

        double age = body.SongMs - body.ImpactMs;
        if (age < 0 || age >= ImpactMs)
            return Vector2.One;

        // Flat against the wall, then a little overshoot back past round.
        float t = (float)(age / ImpactMs);
        float squash = MathF.Cos(t * MathF.PI * 1.5f) * (1f - t);
        return Along(body.ImpactWay, 1f - 0.4f * squash, 1f + 0.35f * squash);
    }

    /// <summary>A squashed body keeps its far edge on the wall, so its centre moves into it, playfield units.</summary>
    private static Vector2 ImpactShift(in CurveBody body)
    {
        if (body.Dashing)
            return Vector2.Zero;
        Vector2 way = body.ImpactWay;
        Vector2 axis = new(MathF.Abs(way.X), MathF.Abs(way.Y));
        float halfAlong = Vector2.Dot(PlayRules.BodySize / 2f, axis);
        float shapeAlong = Vector2.Dot(DashShape(body), axis);
        return way * halfAlong * (1f - shapeAlong);
    }

    /// <summary>A scale of <paramref name="along"/> on the way's axis and <paramref name="across"/> on the other.</summary>
    private static Vector2 Along(Vector2 way, float along, float across) =>
        MathF.Abs(way.X) > MathF.Abs(way.Y) ? new Vector2(along, across) : new Vector2(across, along);

    /// <summary>
    /// Fading copies of the body strung back along the dash, never past where
    /// it went off, so the dash reads as a smear of speed.
    /// </summary>
    private static void DrawAfterimages(IRectCanvas canvas, Handle rid, in CurveBody body, PlayfieldView view, Vector2 center, Vector2 half, float alpha)
    {
        float travelled = Vector2.Dot(body.Position - body.DashFrom, body.Dash);
        Vector2 shape = half * Along(body.Dash, 1.1f, 0.8f);
        for (int i = 1; i <= Afterimages; i++)
        {
            float back = PlayRules.DashSpeed * AfterimageGapMs * i / 1000f;
            if (back > travelled)
                break;

            Vector2 at = center - body.Dash * back * view.Scale;
            float fade = 1f - i / (float)(Afterimages + 1);
            var rect = new Rect2(at - shape, shape * 2f);
            canvas.DrawRect(rid, rect, new Color(BodyFill.R, BodyFill.G, BodyFill.B, 0.5f * fade * alpha));
            canvas.DrawBorder(rid, rect, new Color(1f, 1f, 1f, 0.45f * fade * alpha), 1.5f);
        }
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

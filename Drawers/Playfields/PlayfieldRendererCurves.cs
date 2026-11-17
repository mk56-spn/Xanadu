using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Playfields;
using Xanadu.Ui.Systems.Canvas;
using static Xanadu.Model.Playfields.PlayfieldColors;
using static Xanadu.Model.Playfields.PlayfieldMetrics;

namespace Xanadu.Drawers.Playfields;

/// <summary>Draws curve notes, in both editor and play modes.</summary>
public static partial class PlayfieldRenderer
{
    /// <summary>
    /// A curve note's path, under its head (drawn as the note it is): the line,
    /// a dot on each of its points ringed once, twice or three times for its
    /// 1×/2×/3× speed, and, while the playhead is on it, a ring where the body
    /// is pulled to. A missed head's path is greyed: it steers nothing.
    /// </summary>
    public static void DrawCurve(
        IRectCanvas canvas,
        Handle rid,
        Entity entity,
        PlayfieldView view,
        Playhead playhead,
        bool selected)
    {
        Note note = entity.GetComponent<Note>();
        if (note.Path is not { Count: >= NotePath.MinPoints } curve)
            return;

        CurvePath path = CurvePath.Of(note);
        float alpha = CurveFade.Alpha(path, playhead.TimeMs);
        if (selected)
            alpha = MathF.Max(alpha, 0.5f);
        if (alpha <= 0f)
            return;

        Color own = GroupColor.For(entity, CurveColor);
        Color color = entity.Tags.Has<NoteMissed>()
            ? new Color(0.4f, 0.4f, 0.5f, own.A * alpha * 0.45f)
            : Faded(own, own.A * alpha);
        if (selected)
            DrawCurvePath(canvas, rid, path.Points, view, SelectedNoteColor, CurveWidth(view) + 4f);
        DrawCurvePath(canvas, rid, path.Points, view, color, CurveWidth(view));
        DrawCurvePoints(canvas, rid, curve.Points.ToArray(), curve.Speeds.ToArray(), view, color);

        if (path.Covers(playhead.TimeMs))
        {
            (Vector2 body, _) = path.At(playhead.TimeMs);
            canvas.DrawCircleOutline(rid, view.PlayfieldToScreen(body), NoteSize * view.Scale * 0.3f, Faded(Colors.White, color.A), 2f);
        }
    }

    /// <summary>A path given in playfield points, as one line on screen.</summary>
    public static void DrawCurvePath(
        IRectCanvas canvas,
        Handle rid,
        IReadOnlyList<Vector2> points,
        PlayfieldView view,
        Color color,
        float width)
    {
        if (points.Count < 2)
            return;

        var screen = new Vector2[points.Count];
        for (int i = 0; i < screen.Length; i++)
            screen[i] = view.PlayfieldToScreen(points[i]);
        canvas.DrawPolyline(rid, screen, color, width);
    }

    /// <summary>A dot per point, ringed by its speed.</summary>
    public static void DrawCurvePoints(
        IRectCanvas canvas,
        Handle rid,
        IReadOnlyList<Vector2> points,
        IReadOnlyList<float> speeds,
        PlayfieldView view,
        Color color)
    {
        if (points.Count == 0)
            return;

        float dot = MathF.Max(3f, NoteSize * view.Scale * 0.08f);
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 at = view.PlayfieldToScreen(points[i]);
            canvas.DrawCircle(rid, at, dot, color);
            int rings = SpeedTier(i < speeds.Count ? speeds[i] : Note.DefaultMomentum);
            for (int ring = 1; ring <= rings; ring++)
                canvas.DrawCircleOutline(rid, at, dot + ring * MathF.Max(3f, dot * 0.8f), color, 1.5f);
        }
    }

    /// <summary>1, 2 or 3: which of the strength buttons a speed is nearest.</summary>
    public static int SpeedTier(float speed)
    {
        if (speed < 750f)
            return 1;
        return speed < 1050f ? 2 : 3;
    }

    private static float CurveWidth(PlayfieldView view) => MathF.Max(2f, NoteSize * view.Scale * 0.07f);

    /// <summary>The curve rework's one fixed floor: a band across the field from <see cref="PlayRules.FloorY"/> to its bottom edge.</summary>
    public static void DrawFloor(IRectCanvas canvas, Handle rid, PlayfieldView view, Playfield playfield)
    {
        Vector2 top = view.PlayfieldToScreen(new Vector2(0, PlayRules.FloorY(playfield)));
        Vector2 bottom = view.PlayfieldToScreen(new Vector2(playfield.Width, playfield.Height));
        canvas.DrawRect(rid, new Rect2(top, bottom - top), FloorColor);
        canvas.DrawLine(rid, top, new Vector2(bottom.X, top.Y), FloorEdgeColor, 2f);
    }
}

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

/// <summary>Draws route notes, in both editor and play modes.</summary>
public static partial class PlayfieldRenderer
{
    private const float ArmAlpha = 0.35f;

    /// <summary>
    /// A route note, under its head (drawn as the note it is). In the editor:
    /// the route run straight ahead with a tick at each turn, since the way is
    /// the player's. In play, before the head is hit: a cross on the head, each
    /// arm as long as the first stretch, so its length reads as the time to the
    /// first turn. Once running: the route as taken up to the next open turn,
    /// where faint arms show the three ways on, each as long as the next
    /// stretch. While the playhead is on it, a ring where the body is pulled to.
    /// A missed head is greyed: it steers nothing.
    /// </summary>
    public static void DrawRoute(
        IRectCanvas canvas,
        Handle rid,
        Entity entity,
        PlayfieldView view,
        Playhead playhead,
        bool selected,
        bool editor)
    {
        Note note = entity.GetComponent<Note>();
        if (note.Route is null)
            return;

        bool running = entity.TryGetComponent(out RouteRun run);
        RoutePath path = running ? RoutePath.Of(note, run.Start, run.Turns) : RoutePath.Of(note);
        float alpha = RouteFade.Alpha(path, playhead.TimeMs);
        if (selected)
            alpha = MathF.Max(alpha, 0.5f);
        if (alpha <= 0f)
            return;

        Color own = CurveColor;
        Color color = entity.Tags.Has<NoteMissed>()
            ? new Color(0.4f, 0.4f, 0.5f, own.A * alpha * 0.45f)
            : Faded(own, own.A * alpha);
        Color arm = Faded(color, color.A * ArmAlpha);
        float width = CurveWidth(view);

        if (editor)
        {
            if (selected)
                DrawCurvePath(canvas, rid, path.Points, view, SelectedNoteColor, width + 4f);
            DrawCurvePath(canvas, rid, path.Points, view, color, width);
            for (int corner = 1; corner < path.Points.Count; corner++)
                DrawCornerTick(canvas, rid, view, path.Points[corner], path.HeadingOf(corner - 1), color, corner == path.Points.Count - 1);
            DrawArms(canvas, rid, view, note.Position, path.LengthOf(0), Vector2.Zero, arm, width);
        }
        else if (!running)
        {
            DrawArms(canvas, rid, view, note.Position, path.LengthOf(0), Vector2.Zero, arm, width);
        }
        else
        {
            int open = NextOpenCorner(path, run, playhead.TimeMs);
            var shown = new List<Vector2>(open + 1);
            for (int i = 0; i <= open; i++)
                shown.Add(path.Points[i]);
            DrawCurvePath(canvas, rid, shown, view, color, width);
            if (open - 1 < path.Turns)
                DrawArms(canvas, rid, view, path.Points[open], path.LengthOf(open), path.HeadingOf(open - 1), arm, width);
            else
                DrawCornerTick(canvas, rid, view, path.Points[open], path.HeadingOf(open - 1), color, true);
        }

        if (path.Covers(playhead.TimeMs))
        {
            (Vector2 body, _) = path.At(playhead.TimeMs);
            canvas.DrawCircleOutline(rid, view.PlayfieldToScreen(body), NoteSize * view.Scale * 0.3f, Faded(Colors.White, color.A), 2f);
        }
    }

    /// <summary>
    /// The first corner still to come whose turn is not pressed yet, or the end:
    /// the route is known up to there. Corner <c>c</c> is turn <c>c - 1</c>.
    /// </summary>
    private static int NextOpenCorner(RoutePath path, RouteRun run, long nowMs)
    {
        for (int corner = 1; corner < path.Points.Count - 1; corner++)
        {
            int turn = corner - 1;
            bool pressed = turn < run.Pressed.Length && run.Pressed[turn];
            if (!pressed && path.TimeAt(corner) >= nowMs)
                return corner;
        }

        return path.Points.Count - 1;
    }

    /// <summary>
    /// Faint arms from a point, each <paramref name="length"/> long: all four
    /// ways when <paramref name="into"/> is zero, else straight on and the two right angles.
    /// </summary>
    private static void DrawArms(IRectCanvas canvas, Handle rid, PlayfieldView view, Vector2 from, float length, Vector2 into, Color color, float width)
    {
        if (length <= 0f)
            return;

        Vector2 start = view.PlayfieldToScreen(from);
        ReadOnlySpan<Vector2> ways = [Vector2.UnitX, -Vector2.UnitX, Vector2.UnitY, -Vector2.UnitY];
        foreach (Vector2 way in ways)
        {
            if (into != Vector2.Zero && Vector2.Dot(way, into) < -0.5f)
                continue;
            Vector2 end = view.PlayfieldToScreen(from + way * length);
            canvas.DrawLine(rid, start, end, color, width);
            DrawCornerTick(canvas, rid, view, from + way * length, way, color, false);
        }
    }

    /// <summary>A short bar across the route where a turn (or, longer, the end) comes.</summary>
    private static void DrawCornerTick(IRectCanvas canvas, Handle rid, PlayfieldView view, Vector2 at, Vector2 heading, Color color, bool end)
    {
        Vector2 across = heading == Vector2.Zero ? Vector2.UnitY : new Vector2(-heading.Y, heading.X);
        float half = MathF.Max(4f, NoteSize * view.Scale * (end ? 0.22f : 0.12f));
        Vector2 centre = view.PlayfieldToScreen(at);
        canvas.DrawLine(rid, centre - across * half, centre + across * half, color, end ? 3f : 2f);
    }

    /// <summary>A line given in playfield points, as one line on screen.</summary>
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

    private static float CurveWidth(PlayfieldView view) => MathF.Max(2f, NoteSize * view.Scale * 0.07f);

    /// <summary>The rework's one fixed floor: a band across the field from <see cref="PlayRules.FloorY"/> to its bottom edge.</summary>
    public static void DrawFloor(IRectCanvas canvas, Handle rid, PlayfieldView view, Playfield playfield)
    {
        Vector2 top = view.PlayfieldToScreen(new Vector2(0, PlayRules.FloorY(playfield)));
        Vector2 bottom = view.PlayfieldToScreen(new Vector2(playfield.Width, playfield.Height));
        canvas.DrawRect(rid, new Rect2(top, bottom - top), FloorColor);
        canvas.DrawLine(rid, top, new Vector2(bottom.X, top.Y), FloorEdgeColor, 2f);
    }
}

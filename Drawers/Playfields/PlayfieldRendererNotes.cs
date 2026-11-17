using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;
using Xanadu.Ui.Ecs;
using Xanadu.Ui.Systems.Canvas;
using Position = Xanadu.Core.Position;
using static Xanadu.Model.Playfields.PlayfieldColors;
using static Xanadu.Model.Playfields.PlayfieldMetrics;
using Xanadu.Model.Playfields;

namespace Xanadu.Drawers.Playfields;

public static partial class PlayfieldRenderer
{
    /// <summary>
    /// Draws one note (not a rail note) with its approach ring, dash link,
    /// selection ring and group badge, under its own shader.
    /// </summary>
    public static void DrawNote(
        IRectCanvas canvas,
        Handle rid,
        Entity entity,
        PlayfieldView view,
        Playhead playhead,
        Entity selectedNote)
    {
        float size = NoteSize * view.Scale;
        Note note = entity.GetComponent<Note>();
        // A route note's head stays as long as its route shows, so the route is never headless.
        // A hold's head stays to its tail too, riding the bar while held.
        float alpha = note.IsRoute ? RouteFade.Alpha(RoutePath.Of(note), playhead.TimeMs)
            : note.IsHold ? NoteSpanFade.Alpha(note, playhead.TimeMs)
            : NoteFade.Alpha(note, playhead.TimeMs);
        if (alpha <= 0)
            return;

        Vector2 center = view.PlayfieldToScreen(
            entity.Tags.Has<NoteHolding>() ? HoldPoint(note, playhead.TimeMs) : note.Position);
        float radius = size / 2;
        Color laneColor = ColorFor(note);
        Color color = entity.Tags.Has<NoteMissed>()
            ? new Color(0.4f, 0.4f, 0.5f, alpha * laneColor.A * 0.45f)
            : new Color(laneColor.R, laneColor.G, laneColor.B, alpha * laneColor.A);

        float approach = NoteFade.Approach(note.TimeMs, playhead.TimeMs);
        float approachRadius = approach >= 0 ? ApproachRadius(radius, approach) : 0f;
        var ring = ApproachColor(laneColor, alpha);

        if (approach >= 0)
            canvas.DrawCircleOutline(rid, center, approachRadius, ring, 2);

        DrawNoteBody(canvas, rid, note, center, radius, color);

        if (!selectedNote.IsNull && selectedNote.Id == entity.Id)
        {
            canvas.DrawCircleOutline(
                rid,
                center,
                radius + 1.5f,
                new Color(
                    SelectedNoteColor.R,
                    SelectedNoteColor.G,
                    SelectedNoteColor.B,
                    color.A),
                2);
        }


        canvas.SelectCommonShader("");
        canvas.SelectShader(0);
    }

    private static Color ApproachColor(Color lane, float alpha) => new(lane.R, lane.G, lane.B, alpha * lane.A * 0.75f);

    /// <summary>A wall note: a flat upright slab, meant to sit on a wall's face, with a bright core line.</summary>
    private static void DrawWallShape(IRectCanvas canvas, Handle rid, Vector2 center, float radius, Color color)
    {
        Vector2 size = WallNoteSize(radius);
        var slab = new Rect2(center - size / 2, size);
        canvas.DrawRect(rid, slab, color with { A = color.A * 0.55f });
        canvas.DrawBorder(rid, slab, color, Mathf.Max(1.5f, radius * 0.08f));
        canvas.DrawLine(rid, center - new Vector2(0, size.Y / 2 - radius * 0.2f), center + new Vector2(0, size.Y / 2 - radius * 0.2f), NoteEdgeColor with { A = color.A }, Mathf.Max(1f, radius * 0.1f));
    }

    /// <summary>Draws just a note's body at a position, shared by play and the editor's group preview.</summary>
    public static void DrawNoteBody(
        IRectCanvas canvas,
        Handle rid,
        Note note,
        Vector2 center,
        float radius,
        Color color)
    {
        if (note.IsDash)
            DrawDashShape(canvas, rid, center, radius, color);
        else
            DrawHitShape(canvas, rid, center, radius, color);
    }

    /// <summary>Where along a hold's bar the song is, from its head at its time to its tail at its end.</summary>
    private static Vector2 HoldPoint(Note note, long playheadMs)
    {
        long length = note.EndMs - note.TimeMs;
        float t = length > 0 ? Math.Clamp((playheadMs - note.TimeMs) / (float)length, 0f, 1f) : 1f;
        return Vector2.Lerp(note.Position, note.TailPosition, t);
    }

    /// <summary>
    /// A hold's bar, under its head: a wide band in the key's colour from the
    /// head to a ring at the tail. Only for looks: the hold only checks the key.
    /// While held, the part already behind the head is bright.
    /// </summary>
    public static void DrawHoldBar(IRectCanvas canvas, Handle rid, Entity entity, PlayfieldView view, Playhead playhead, bool selected)
    {
        Note note = entity.GetComponent<Note>();
        float alpha = NoteSpanFade.Alpha(note, playhead.TimeMs);
        if (selected)
            alpha = MathF.Max(alpha, 0.5f);
        if (alpha <= 0 || !note.IsHold)
            return;

        Color lane = ColorFor(note);
        bool missed = entity.Tags.Has<NoteMissed>();
        Color color = missed
            ? new Color(0.4f, 0.4f, 0.5f, alpha * 0.3f)
            : new Color(lane.R, lane.G, lane.B, alpha * lane.A * 0.4f);
        Vector2 head = view.PlayfieldToScreen(note.Position);
        Vector2 tail = view.PlayfieldToScreen(note.TailPosition);
        float width = NoteSize * view.Scale * 0.55f;
        float radius = NoteSize * view.Scale / 2f;

        if (selected)
            canvas.DrawLine(rid, head, tail, Faded(SelectedNoteColor, alpha), width + 6f);
        canvas.DrawLine(rid, head, tail, color, width);
        canvas.DrawCircle(rid, tail, width / 2f, color);
        canvas.DrawCircleOutline(rid, tail, radius * 0.8f, Faded(NoteEdgeColor, color.A * 1.6f), 3f);

        if (entity.Tags.Has<NoteHolding>())
        {
            Vector2 at = view.PlayfieldToScreen(HoldPoint(note, playhead.TimeMs));
            canvas.DrawLine(rid, head, at, new Color(lane.R, lane.G, lane.B, alpha * lane.A), width);
        }
    }

    /// <summary>The editor's mark of how far a dash teleports: a faint ring that far round the note.</summary>
    public static void DrawDashReach(IRectCanvas canvas, Handle rid, Note note, PlayfieldView view, Playhead playhead, bool selected)
    {
        float alpha = MathF.Max(NoteFade.Alpha(note, playhead.TimeMs), selected ? 0.5f : 0f);
        if (alpha <= 0 || !note.IsDash)
            return;

        Color color = selected ? SelectedNoteColor : NeutralColor;
        canvas.DrawCircleOutline(rid, view.PlayfieldToScreen(note.Position), note.DashLength * view.Scale, Faded(color, alpha * 0.35f), 2f);
    }

    private static Color Faded(Color color, float alpha) => new(color.R, color.G, color.B, alpha);
}

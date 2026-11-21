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
        float alpha = NoteFade.Alpha(note, playhead.TimeMs);
        if (alpha <= 0)
            return;

        Vector2 center = view.PlayfieldToScreen(note.Position);
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
        canvas.DrawBorder(rid, slab, color, Math.Max(1.5f, radius * 0.08f));
        canvas.DrawLine(rid, center - new Vector2(0, size.Y / 2 - radius * 0.2f), center + new Vector2(0, size.Y / 2 - radius * 0.2f), NoteEdgeColor with { A = color.A }, Math.Max(1f, radius * 0.1f));
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
        DrawHitShape(canvas, rid, center, radius, color);
    }

    private static Color Faded(Color color, float alpha) => new(color.R, color.G, color.B, alpha);
}

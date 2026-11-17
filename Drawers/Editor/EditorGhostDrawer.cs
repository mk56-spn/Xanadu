using Xanadu.Ui.Systems.Canvas;
using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Playfields;
using Xanadu.Drawers.Playfields;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Model.Editor;

namespace Xanadu.Drawers.Editor;

/// <summary>A faint copy of what a click would place, under the pointer.</summary>
public sealed class EditorGhostDrawer(EditorDrawSurface surface)
{
    private const float GhostAlpha = 0.45f;

    /// <summary>
    /// The route being laid when there is one; otherwise the head a click would
    /// start, in its key's colour, under the pointer.
    /// </summary>
    public void Draw(EditorFrame frame)
    {
        Entity editor = frame.Entity;
        if (editor.TryGetComponent(out EditorRouteDraft draft) && draft.Active)
        {
            DrawRouteDraft(editor, frame, draft);
            return;
        }

        if (editor.TryGetComponent(out EditorDragDraft drag) && drag.Active)
        {
            DrawPreview(EditorPlacementNote.Build(editor, drag.StartMs, drag.Head, drag.Current), frame.View, 1f);
            return;
        }

        if (!editor.TryGetComponent(out EditorPlacementGhost ghost) || !ghost.Visible)
            return;

        PlayfieldView view = frame.View;
        Vector2 pointer = view.ScreenToPlayfield(ghost.Screen);
        Vector2 at = ViewportSnap.IsOn(surface.Entities) ? PlayfieldGrid.NearestCrossing(pointer, frame.Playfield) : pointer;
        DrawPreview(EditorPlacementNote.Build(editor, frame.Playhead.TimeMs, at), view, GhostAlpha);
    }

    /// <summary>A note the tool would place: a dash with its reach ring, a hold with its bar, then the head.</summary>
    private void DrawPreview(Note note, PlayfieldView view, float alpha)
    {
        Vector2 head = view.PlayfieldToScreen(note.Position);
        Color color = PlayfieldColors.ColorFor(note);
        if (note.IsDash)
            surface.Canvas.DrawCircleOutline(surface.Target, head, note.DashLength * view.Scale, Fade(color, alpha * 0.5f), 2f);
        if (note.IsHold)
        {
            Vector2 tail = view.PlayfieldToScreen(note.TailPosition);
            float width = PlayfieldMetrics.NoteSize * view.Scale * 0.55f;
            surface.Canvas.DrawLine(surface.Target, head, tail, Fade(color, alpha * 0.4f), width);
            surface.Canvas.DrawCircle(surface.Target, tail, width / 2f, Fade(color, alpha * 0.4f));
        }

        PlayfieldRenderer.DrawNoteBody(
            surface.Canvas,
            surface.Target,
            note,
            head,
            PlayfieldMetrics.NoteSize * view.Scale / 2f,
            Fade(color, alpha));
    }

    private static Color Fade(Color color, float alpha) => new(color.R, color.G, color.B, color.A * alpha);

    /// <summary>
    /// The route the route tool is laying: run straight ahead with a tick at
    /// each turn so far, and a faint line on to where the playhead would add the next.
    /// </summary>
    private void DrawRouteDraft(Entity editor, EditorFrame frame, EditorRouteDraft draft)
    {
        PlayfieldView view = frame.View;
        var times = new List<long>(draft.TimesMs);
        long pending = EditorTiming.Snap(frame.Playhead.TimeMs, frame.Playhead) - draft.StartMs;
        bool adds = pending > (times.Count > 0 ? times[^1] : 0);

        Vector2 head = view.PlayfieldToScreen(draft.Head);
        float speed = NoteRoute.SpeedOf(draft.Strength);
        if (adds)
        {
            Vector2 next = view.PlayfieldToScreen(draft.Head + Vector2.UnitX * (speed * pending / 1000f));
            surface.Canvas.DrawLine(surface.Target, head, next, Faint(PlayfieldColors.CurveColor), 2f);
            surface.Canvas.DrawCircleOutline(surface.Target, next, 6f, Palette.Accent, 2f);
        }

        if (times.Count > 0)
        {
            var path = RoutePath.Of(new Note(draft.StartMs, draft.Head, route: new NoteRoute(draft.Strength, times.ToArray())));
            PlayfieldRenderer.DrawCurvePath(surface.Canvas, surface.Target, path.Points, view, PlayfieldColors.CurveColor, 3f);
            for (int i = 1; i < path.Points.Count; i++)
                surface.Canvas.DrawCircle(surface.Target, view.PlayfieldToScreen(path.Points[i]), 4f, PlayfieldColors.CurveColor);
        }

        Note note = EditorPlacementNote.Build(editor, draft.StartMs, draft.Head);
        PlayfieldRenderer.DrawNoteBody(
            surface.Canvas,
            surface.Target,
            note,
            head,
            PlayfieldMetrics.NoteSize * view.Scale / 2f,
            PlayfieldColors.ColorFor(note));
    }

    private static Color Faint(Color color) => new(color.R, color.G, color.B, color.A * GhostAlpha);
}

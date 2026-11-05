using System;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Playfields;

namespace Xanadu.Systems.Editor;

/// <summary>
/// The editor's playfield items, in the order the editor stacks them: the
/// terrain first (polygons, blocks, spikes, grass, texts), then what sits on
/// the timeline (notes, rails, mines, start points).
/// </summary>
public sealed class EditorItemsDrawer(EditorDrawSurface surface)
{
    public void DrawTerrain(EditorFrame frame)
    {
        PlayfieldView view = frame.View;
        Func<Entity, bool> isSelected = frame.Selection.Contains;
        PlayfieldRenderer.DrawPolygons(surface.Canvas, surface.Target, surface.Entities, view, isSelected);
        PlayfieldRenderer.DrawBlocks(surface.Canvas, surface.Target, surface.Entities, view, isSelected);
        PlayfieldRenderer.DrawSpikes(surface.Canvas, surface.Target, surface.Entities, view, isSelected);
        PlayfieldRenderer.DrawGrass(surface.Canvas, surface.Target, surface.Entities, view, frame.Seconds, isSelected);
        PlayfieldRenderer.DrawTexts(surface.Canvas, surface.Target, surface.Entities, view, isSelected);
    }

    public void DrawTimed(EditorFrame frame)
    {
        PlayfieldView view = frame.View;
        PlayfieldRenderer.DrawNotes(
            surface.Canvas,
            surface.Target,
            surface.Entities,
            view,
            frame.Playfield,
            frame.Playhead,
            frame.Selection.Note);
        PlayfieldRenderer.DrawRails(
            surface.Canvas,
            surface.Target,
            surface.Entities,
            view,
            frame.Playfield,
            frame.Playhead,
            previewBoth: true);
        PlayfieldRenderer.DrawMines(
            surface.Canvas,
            surface.Target,
            surface.Entities,
            view,
            frame.Playhead);
        DrawStartPoints(view, frame.Selection);
    }

    private void DrawStartPoints(PlayfieldView view, NoteSelection selection)
    {
        foreach (Entity entity in surface.Entities.Query<StartPoint>().ToEntityList())
        {
            StartPoint start = entity.GetComponent<StartPoint>();
            Vector2 center = view.PlayfieldToScreen(start.Position);
            float radius = PlayfieldRenderer.StartPointRadius(view);
            PlayfieldRenderer.DrawStartPointBody(
                surface.Canvas,
                surface.Target,
                center,
                radius,
                PlayfieldRenderer.StartPointColor);

            // Selected and grouped the way a note is.
            if (selection.Contains(entity))
            {
                surface.Canvas.DrawCircleOutline(
                    surface.Target,
                    center,
                    radius + 4f,
                    PlayfieldRenderer.SelectedNoteColor,
                    2);
            }

            if (entity.TryGetComponent(out ItemGroups groups) && groups.Mask != 0)
            {
                PlayfieldRenderer.DrawGroupBadge(
                    surface.Canvas,
                    surface.Target,
                    center + new Vector2(radius * 0.78f, -radius * 0.78f),
                    groups.Mask,
                    1f);
            }
        }
    }
}

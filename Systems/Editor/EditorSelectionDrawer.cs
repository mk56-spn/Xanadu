using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Playfields;

namespace Xanadu.Systems.Editor;

/// <summary>What marks the selection: outlines, resize nubs, the rotate handle and the marquee.</summary>
public sealed class EditorSelectionDrawer(EditorDrawSurface surface)
{
    /// <summary>
    /// Outlines the selection whatever the playhead's time, so an item stays
    /// marked after scrolling past the point where its body fades out.
    /// </summary>
    public void DrawOutlines(PlayfieldView view, NoteSelection selection)
    {
        float size = PlayfieldRenderer.NoteSize * view.Scale;

        foreach (Entity entity in surface.Entities.Query<Note>().ToEntityList())
        {
            if (!selection.Contains(entity))
                continue;

            Note note = entity.GetComponent<Note>();
            Vector2 center = view.PlayfieldToScreen(GroupOffset.Shown(entity, note.Position));
            surface.Canvas.DrawCircleOutline(
                surface.Target,
                center,
                size / 2 + 1.5f,
                PlayfieldRenderer.SelectedNoteColor,
                2);
        }

        float mineSize = PlayfieldRenderer.MineSize * view.Scale;
        foreach (Entity entity in surface.Entities.Query<Mine>().ToEntityList())
        {
            if (!selection.Contains(entity))
                continue;

            Mine mine = entity.GetComponent<Mine>();
            Vector2 center = view.PlayfieldToScreen(GroupOffset.Shown(entity, mine.Position));
            surface.Canvas.DrawCircleOutline(
                surface.Target,
                center,
                mineSize / 2 + 1.5f,
                PlayfieldRenderer.SelectedNoteColor,
                2);
        }
    }

    /// <summary>
    /// Outlines every screen-read region over the items, marked brighter when
    /// selected. Regions are authoring boundaries: they draw no body in play,
    /// and the editor draws them here, after the item pass, so their outline
    /// never becomes part of a later screen read.
    /// </summary>
    public void DrawRegions(PlayfieldView view, NoteSelection selection)
    {
        foreach (Entity entity in surface.Entities.Query<ScreenReadRegion>().ToEntityList())
        {
            ScreenReadRegion region = entity.GetComponent<ScreenReadRegion>();
            PlayfieldView shown = GroupOffset.View(entity, view);
            var rect = new Rect2(
                shown.PlayfieldToScreen(region.Position),
                region.Size * shown.Scale);
            bool selected = selection.Contains(entity);
            Color color = selected
                ? PlayfieldRenderer.BlockSelectedEdge
                : PlayfieldRenderer.PolygonFill;
            surface.Canvas.DrawBorder(surface.Target, rect, color, selected ? 2.5f : 1.5f);
            surface.Canvas.DrawTextLeft(surface.Target, rect.Grow(-4), "SCREEN READ", color);
        }
    }

    public void DrawBox(UiMarqueeBox box) => CanvasMarquee.Draw(surface.Canvas, surface.Target, box);

    /// <summary>The grips that resize the primary selection and the handle that turns it.</summary>
    public void DrawHandles(PlayfieldView view, NoteSelection selection)
    {
        DrawGridHandles(view, selection);
        DrawRotationHandle(view, selection);
    }

    /// <summary>
    /// Draws one nub at the middle of each side and at each corner of the primary
    /// selected boxed item. They are the grips the pointer grabs to resize it.
    /// </summary>
    private void DrawGridHandles(PlayfieldView view, NoteSelection selection)
    {
        Entity primary = selection.Note;
        if (!EditorItems.IsItem(primary) || !GridPlacement.TryGet(primary, out Rect2 bounds, out float degrees))
            return;

        view = GroupOffset.View(primary, view);

        foreach (GridHandle handle in GridPlacement.Handles)
        {
            Rect2 nub = GridPlacement.HandleScreenRect(bounds, handle, degrees, view);
            surface.Canvas.DrawRect(surface.Target, nub, PlayfieldRenderer.HandleEdge);
            surface.Canvas.DrawRect(surface.Target, nub.Grow(-2), PlayfieldRenderer.BlockSelectedEdge);
        }
    }

    private void DrawRotationHandle(PlayfieldView view, NoteSelection selection)
    {
        // Shown with the primary item's group move, as the rotation tool picks it.
        view = GroupOffset.View(selection.Note, view);
        Vector2 pivot;
        Vector2 handle;
        EntityList rotating = surface.Entities.Query<EditorSelectionRotation>().ToEntityList();
        if (rotating.Count > 0)
        {
            // Mid-drag the selection's box keeps changing, so the handle swings
            // around the pivot that was fixed when the drag began.
            EditorSelectionRotation rotation = rotating[0].GetComponent<EditorSelectionRotation>();
            pivot = view.PlayfieldToScreen(rotation.Pivot);
            handle = pivot + rotation.Arm.Rotated(rotation.Angle);
        }
        else
        {
            if (!SelectionRotation.TryGetBounds(selection.AllNotes, out Rect2 bounds))
                return;

            pivot = view.PlayfieldToScreen(bounds.GetCenter());
            handle = SelectionRotation.HandlePoint(bounds, view);
        }

        Color accent = PlayfieldRenderer.BlockSelectedEdge;
        surface.Canvas.DrawLine(surface.Target, pivot, handle, accent, 1);
        surface.Canvas.DrawCircle(surface.Target, pivot, 3, accent);
        surface.Canvas.DrawCircle(surface.Target, handle, SelectionRotation.HandleRadius, accent);
        surface.Canvas.DrawCircleOutline(
            surface.Target,
            handle,
            SelectionRotation.HandleRadius,
            PlayfieldRenderer.HandleEdge,
            2);
    }
}

using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Playfields;
using Xanadu.Ui.Ecs;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Drawers.Editor;

/// <summary>What marks the selection: a ring round each selected note's head, and the marquee.</summary>
public sealed class EditorSelectionDrawer(EditorDrawSurface surface)
{
    /// <summary>
    /// Outlines the selection whatever the playhead's time, so a note stays
    /// marked after scrolling past the point where its body fades out.
    /// </summary>
    public void DrawOutlines(PlayfieldView view, NoteSelection selection) =>
        surface.Entities.Query<Note>().EachEntity(new Outline(surface, view, selection, surface.Entities));

    public void DrawBox(UiMarqueeBox box) => CanvasMarquee.Draw(surface.Canvas, surface.Target, box);

    private readonly struct Outline(EditorDrawSurface surface, PlayfieldView view, NoteSelection selection, EntityStore store) : IEachEntity<Note>
    {
        public void Execute(ref Note note, int id)
        {
            if (!selection.Contains(store.GetEntityById(id)))
                return;

            surface.Canvas.DrawCircleOutline(
                surface.Target,
                view.PlayfieldToScreen(note.Position),
                PlayfieldMetrics.NoteSize * view.Scale / 2 + 1.5f,
                PlayfieldColors.SelectedNoteColor,
                2);
        }
    }
}

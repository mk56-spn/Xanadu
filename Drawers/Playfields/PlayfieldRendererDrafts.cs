using Friflo.Engine.ECS;
using Xanadu.Core.Items;
using Xanadu.Model.Animation;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Playfields;
using Xanadu.Ui.Systems.Canvas;
using static Xanadu.Model.Playfields.PlayfieldColors;

namespace Xanadu.Drawers.Playfields;

/// <summary>
/// Updrafts (<see cref="Draft"/>): each one's look is a Xif (<see cref="Drafts.LookFor"/>:
/// a vented emitter, a column of air fading up past its hover, wisps rising
/// through it with the song, a bar at the hover), drawn by
/// the Xif drawer with the song's clock and the hover set on it. In the editor
/// the hover bar is brighter and the selected draft is outlined.
/// </summary>
public static partial class PlayfieldRenderer
{
    public static void DrawDrafts(IRectCanvas canvas, Handle rid, EntityStore entities, in PlayfieldItemFrame frame)
    {
        int selected = frame.Editor && !frame.SelectedNote.IsNull ? frame.SelectedNote.Id : 0;
        entities.Query<Draft>().EachEntity(new DraftDraw(canvas, rid, frame.View, frame.Playhead.ShownMs, frame.Editor, selected));
    }

    /// <summary>
    /// One draft: its look (<see cref="Drafts.LookFor"/>) drawn by the Xif drawer
    /// at its vent, with the song's clock and its hover set on the look first;
    /// outlined when selected.
    /// </summary>
    private static void DrawDraft(IRectCanvas canvas, Handle rid, in Draft draft, PlayfieldView view, double now, bool editor, bool selected)
    {
        Xif look = Drafts.LookFor(draft.Height);
        Drafts.SetState(look, (float)(now / 1000d), Drafts.HoverY(draft) - draft.Position.Y, editor ? 0.9f : 0.35f);
        Vector2 vent = view.PlayfieldToScreen(draft.Position);
        var placement = new Transform2D(new Vector2(view.Scale, 0f), new Vector2(0f, view.Scale), vent);
        XifDrawer.Shared.DrawNode(canvas, rid, look, placement, view.Scale, XifTint.Plain);

        if (selected)
        {
            float half = Drafts.Diameter * view.Scale / 2f;
            float height = draft.Height * view.Scale;
            canvas.DrawBorder(rid, new Rect2(vent.X - half, vent.Y - height, half * 2f, height).Grow(4f), SelectedNoteColor, 2f);
        }
    }

    private readonly struct DraftDraw(IRectCanvas canvas, Handle rid, PlayfieldView view, double now, bool editor, int selected)
        : IEachEntity<Draft>
    {
        public void Execute(ref Draft draft, int id) =>
            DrawDraft(canvas, rid, draft, view, now, editor, id == selected);
    }
}

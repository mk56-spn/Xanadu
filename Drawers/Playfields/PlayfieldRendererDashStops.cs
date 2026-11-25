using Friflo.Engine.ECS;
using Xanadu.Model.Animation;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Playfields;
using Xanadu.Ui.Systems.Canvas;
using static Xanadu.Model.Playfields.PlayfieldColors;

namespace Xanadu.Drawers.Playfields;

/// <summary>
/// Dash enders (<see cref="DashStop"/>): each one's look is a Xif
/// (<see cref="DashStops.Look"/>: an orange see-through block with a bar across
/// its middle each way, a stop sign for dashes), drawn by the Xif drawer.
/// Only drawn in its song span (<see cref="SongSpans"/>); the editor shows it faint outside it.
/// Selected in the editor, it is outlined.
/// </summary>
public static partial class PlayfieldRenderer
{
    public static void DrawDashStops(IRectCanvas canvas, Handle rid, EntityStore entities, in PlayfieldItemFrame frame)
    {
        int selected = frame.Editor && !frame.SelectedNote.IsNull ? frame.SelectedNote.Id : 0;
        entities.Query<DashStop>().EachEntity(new DashStopDraw(canvas, rid, frame.View, frame.Playhead.ShownMs, frame.Editor, selected));
    }

    /// <summary>One dash ender: its look (<see cref="DashStops.Look"/>) drawn by the Xif drawer at its place; outlined when selected.</summary>
    private static void DrawDashStop(IRectCanvas canvas, Handle rid, in DashStop stop, PlayfieldView view, double now, bool editor, bool selected)
    {
        float alpha = ItemAlpha(stop.FromMs, stop.ToMs, now, editor);
        if (alpha <= 0f)
            return;
        Vector2 centre = view.PlayfieldToScreen(stop.Position);
        var placement = new Transform2D(new Vector2(view.Scale, 0f), new Vector2(0f, view.Scale), centre);
        XifDrawer.Shared.DrawNode(canvas, rid, DashStops.Look, placement, view.Scale, XifTint.Faded(alpha));

        if (selected)
        {
            Vector2 half = DashStops.Size * view.Scale / 2f;
            canvas.DrawBorder(rid, new Rect2(centre - half, half * 2f).Grow(4f), SelectedNoteColor, 2f);
        }
    }

    /// <summary>
    /// How shown a pad or a dash ender is: by its song span in play, and in the
    /// editor never less than faint, so it can still be found and picked.
    /// </summary>
    private static float ItemAlpha(long fromMs, long toMs, double now, bool editor)
    {
        float alpha = SongSpans.Alpha(fromMs, toMs, now);
        return editor ? Math.Max(alpha, 0.2f) : alpha;
    }

    private readonly struct DashStopDraw(IRectCanvas canvas, Handle rid, PlayfieldView view, double now, bool editor, int selected)
        : IEachEntity<DashStop>
    {
        public void Execute(ref DashStop stop, int id) =>
            DrawDashStop(canvas, rid, stop, view, now, editor, id == selected);
    }
}

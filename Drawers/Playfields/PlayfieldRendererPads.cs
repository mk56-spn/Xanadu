using Friflo.Engine.ECS;
using Xanadu.Model.Animation;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Playfields;
using Xanadu.Ui.Systems.Canvas;
using static Xanadu.Model.Playfields.PlayfieldColors;

namespace Xanadu.Drawers.Playfields;

/// <summary>
/// Pads (<see cref="Pad"/>): each one's look is a Xif (<see cref="Pads.Look"/>:
/// a low glowing slab with chevrons pointing up that flashes and springs when it
/// fires, with a timed pad's approach ring), drawn by the Xif drawer with the
/// pad's state set on it, only in its song span (<see cref="SongSpans"/>; the
/// editor shows it faint outside); in the editor the selected one is outlined.
/// </summary>
public static partial class PlayfieldRenderer
{
    public static void DrawPads(IRectCanvas canvas, Handle rid, EntityStore entities, in PlayfieldItemFrame frame)
    {
        int selected = frame.Editor && !frame.SelectedNote.IsNull ? frame.SelectedNote.Id : 0;
        entities.Query<Pad>().EachEntity(new PadDraw(canvas, rid, frame.View, frame.Playhead.ShownMs, frame.Editor, selected));
    }

    /// <summary>
    /// One pad: its look (<see cref="Pads.Look"/>) drawn by the Xif drawer at its
    /// place, with its spring and timing set on the look first; outlined when selected.
    /// </summary>
    private static void DrawPad(IRectCanvas canvas, Handle rid, in Pad pad, PlayfieldView view, double now, bool selected, bool editor)
    {
        float alpha = ItemAlpha(pad.FromMs, pad.ToMs, now, editor);
        if (alpha <= 0f)
            return;
        double age = now - pad.FiredMs;
        float spring = age >= 0 && age < Pads.SpringMs ? 1f - (float)(age / Pads.SpringMs) : 0f;
        float approach = -1f, ring = 0f;
        if (pad.IsTimed)
        {
            double left = (pad.TimeMs - now) / Pads.ApproachMs;
            if (left > 0 && left <= 1)
                approach = (float)left;
            if (editor)
                ring = Math.Abs(pad.TimeMs - now) <= Pads.ApproachMs ? 0.9f : 0.35f;
        }

        Pads.SetState(spring, approach, ring);
        Vector2 centre = view.PlayfieldToScreen(pad.Position);
        var placement = new Transform2D(new Vector2(view.Scale, 0f), new Vector2(0f, view.Scale), centre);
        XifDrawer.Shared.DrawNode(canvas, rid, Pads.Look, placement, view.Scale, XifTint.Faded(alpha));

        if (selected)
        {
            Vector2 half = Pads.Size * view.Scale / 2f;
            canvas.DrawBorder(rid, new Rect2(centre - half, half * 2f).Grow(4f), SelectedNoteColor, 2f);
        }
    }

    private readonly struct PadDraw(IRectCanvas canvas, Handle rid, PlayfieldView view, double now, bool editor, int selected)
        : IEachEntity<Pad>
    {
        public void Execute(ref Pad pad, int id) =>
            DrawPad(canvas, rid, pad, view, now, id == selected, editor);
    }
}

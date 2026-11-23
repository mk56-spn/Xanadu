using System;
using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
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
/// Selected in the editor, it is outlined.
/// </summary>
public static partial class PlayfieldRenderer
{
    public static void DrawDashStops(IRectCanvas canvas, Handle rid, EntityStore entities, in PlayfieldItemFrame frame)
    {
        foreach (Entity entity in entities.Query<DashStop>().Entities)
        {
            bool selected = frame.Editor && !frame.SelectedNote.IsNull && frame.SelectedNote.Id == entity.Id;
            DrawDashStop(canvas, rid, entity.GetComponent<DashStop>(), frame.View, selected);
        }
    }

    /// <summary>One dash ender: its look (<see cref="DashStops.Look"/>) drawn by the Xif drawer at its place; outlined when selected.</summary>
    private static void DrawDashStop(IRectCanvas canvas, Handle rid, in DashStop stop, PlayfieldView view, bool selected)
    {
        Vector2 centre = view.PlayfieldToScreen(stop.Position);
        var placement = new Transform2D(new Vector2(view.Scale, 0f), new Vector2(0f, view.Scale), centre);
        XifDrawer.Shared.DrawNode(canvas, rid, DashStops.Look, placement, view.Scale, XifTint.Plain);

        if (selected)
        {
            Vector2 half = DashStops.Size * view.Scale / 2f;
            canvas.DrawBorder(rid, new Rect2(centre - half, half * 2f).Grow(4f), SelectedNoteColor, 2f);
        }
    }
}

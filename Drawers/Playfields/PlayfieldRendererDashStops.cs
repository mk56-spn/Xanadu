using System;
using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Playfields;
using Xanadu.Ui.Systems.Canvas;
using static Xanadu.Model.Playfields.PlayfieldColors;

namespace Xanadu.Drawers.Playfields;

/// <summary>
/// Dash enders (<see cref="DashStop"/>): an orange square block with a bar
/// across its middle each way, a stop sign for dashes. Selected in the editor,
/// it is outlined.
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

    private static void DrawDashStop(IRectCanvas canvas, Handle rid, in DashStop stop, PlayfieldView view, bool selected)
    {
        float scale = view.Scale;
        Vector2 centre = view.PlayfieldToScreen(stop.Position);
        Vector2 half = DashStops.Size * scale / 2f;
        var block = new Rect2(centre - half, half * 2f);

        DrawSoftEllipse(canvas, rid, centre, half.X * 1.5f, half.Y * 1.5f, DashStopColor with { A = 0.18f });
        canvas.DrawRect(rid, block, DashStopColor with { A = 0.22f });
        canvas.DrawBorder(rid, block, DashStopColor with { A = 0.95f }, MathF.Max(1.5f, 3f * scale));

        // A bar each way through the middle: a dash ends on the centre, whichever way it came.
        float bar = MathF.Max(1.5f, 4f * scale);
        canvas.DrawRect(rid, new Rect2(centre.X - half.X * 0.55f, centre.Y - bar / 2f, half.X * 1.1f, bar), DashStopColor with { A = 0.9f });
        canvas.DrawRect(rid, new Rect2(centre.X - bar / 2f, centre.Y - half.Y * 0.55f, bar, half.Y * 1.1f), DashStopColor with { A = 0.9f });

        if (selected)
            canvas.DrawBorder(rid, block.Grow(4f), SelectedNoteColor, 2f);
    }
}

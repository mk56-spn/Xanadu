using System;
using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Playfields;
using Xanadu.Ui.Systems.Canvas;
using static Xanadu.Model.Playfields.PlayfieldColors;

namespace Xanadu.Drawers.Playfields;

/// <summary>
/// Pads (<see cref="Pad"/>): a low slab with chevrons pointing up, glowing. When
/// one fires it flashes, its chevrons spring up off it and a column of light
/// shoots the way it launches; in the editor the selected one is outlined.
/// </summary>
public static partial class PlayfieldRenderer
{
    public static void DrawPads(IRectCanvas canvas, Handle rid, EntityStore entities, in PlayfieldItemFrame frame)
    {
        double now = frame.Playhead.ShownMs;
        foreach (Entity entity in entities.Query<Pad>().Entities)
        {
            Pad pad = entity.GetComponent<Pad>();
            bool selected = frame.Editor && !frame.SelectedNote.IsNull && frame.SelectedNote.Id == entity.Id;
            DrawPad(canvas, rid, pad, frame.View, now, selected, frame.Editor);
        }
    }

    private static void DrawPad(IRectCanvas canvas, Handle rid, in Pad pad, PlayfieldView view, double now, bool selected, bool editor)
    {
        float scale = view.Scale;
        Vector2 centre = view.PlayfieldToScreen(pad.Position);
        Vector2 half = Pads.Size * scale / 2f;
        if (pad.IsTimed)
            DrawPadTiming(canvas, rid, pad, centre, half, now, editor);
        double age = now - pad.FiredMs;
        float spring = age >= 0 && age < Pads.SpringMs ? 1f - (float)(age / Pads.SpringMs) : 0f;

        // Glow under it, and a column of light the way it launches when it fires.
        DrawSoftEllipse(canvas, rid, centre, half.X * 1.4f, half.Y * 3f, PadColor with { A = 0.25f + 0.4f * spring });
        if (spring > 0f)
        {
            float height = half.X * 4f * (1f - spring * spring);
            canvas.DrawGradientRect(rid, new Rect2(centre.X - half.X * 0.5f, centre.Y - half.Y - height, half.X, height),
                PadColor with { A = 0f }, PadColor with { A = 0.6f * spring }, horizontal: false);
        }

        var slab = new Rect2(centre - half, half * 2f);
        canvas.DrawRect(rid, slab, PadColor with { A = 0.9f });
        canvas.DrawBorder(rid, slab, new Color(1f, 1f, 1f, 0.7f + 0.3f * spring), MathF.Max(1f, 2f * scale));
        if (spring > 0f)
            canvas.DrawRect(rid, slab, new Color(1f, 1f, 1f, 0.7f * spring));

        // Three chevrons over the slab, springing up as it fires.
        float lift = half.Y * (1.2f + 3f * spring);
        for (int i = 0; i < 3; i++)
        {
            float x = centre.X + (i - 1) * half.X * 0.55f;
            float y = centre.Y - half.Y - lift * (0.6f + 0.25f * i);
            float w = half.X * 0.22f, h = half.Y * 0.9f;
            Color chevron = new(1f, 1f, 1f, 0.55f + 0.45f * spring);
            canvas.DrawLine(rid, new Vector2(x - w, y + h), new Vector2(x, y), chevron, MathF.Max(2f, 3f * scale));
            canvas.DrawLine(rid, new Vector2(x, y), new Vector2(x + w, y + h), chevron, MathF.Max(2f, 3f * scale));
        }

        if (selected)
            canvas.DrawBorder(rid, slab.Grow(4f), SelectedNoteColor, 2f);
    }

    /// <summary>
    /// A timed pad's timing: an ellipse closing onto it over the approach, so
    /// the launch can be read like a note; in the editor a faint ring always,
    /// brighter near its time, marks it as timed.
    /// </summary>
    private static void DrawPadTiming(IRectCanvas canvas, Handle rid, in Pad pad, Vector2 centre, Vector2 half, double now, bool editor)
    {
        double left = (pad.TimeMs - now) / Pads.ApproachMs;
        if (editor)
        {
            bool near = Math.Abs(pad.TimeMs - now) <= Pads.ApproachMs;
            canvas.DrawCircleOutline(rid, centre, half.X * 1.3f, PadColor with { A = near ? 0.9f : 0.35f }, 2f);
        }

        if (left <= 0 || left > 1)
            return;

        float t = (float)left;
        canvas.DrawCircleOutline(rid, centre, half.X * (1.1f + 2.2f * t), new Color(1f, 1f, 1f, 0.9f * (1f - t)), 2f + 3f * (1f - t));
    }
}

using System;
using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Playfields;
using Xanadu.Ui.Systems.Canvas;
using static Xanadu.Model.Playfields.PlayfieldColors;

namespace Xanadu.Drawers.Playfields;

/// <summary>
/// Updrafts (<see cref="Draft"/>): a vented emitter, a column of air fading up
/// past its hover, and wisps rising through it with the song; a short bar marks
/// the hover, bobbing on the beat. In the editor the hover bar is brighter and
/// the selected draft is outlined.
/// </summary>
public static partial class PlayfieldRenderer
{
    private const int DraftWisps = 7;

    public static void DrawDrafts(IRectCanvas canvas, Handle rid, EntityStore entities, in PlayfieldItemFrame frame)
    {
        double now = frame.Playhead.ShownMs;
        double beat = frame.Playhead.Tempo.BeatAt(now);
        foreach (Entity entity in entities.Query<Draft>().Entities)
        {
            bool selected = frame.Editor && !frame.SelectedNote.IsNull && frame.SelectedNote.Id == entity.Id;
            DrawDraft(canvas, rid, entity.GetComponent<Draft>(), frame.View, now, beat, frame.Editor, selected);
        }
    }

    private static void DrawDraft(IRectCanvas canvas, Handle rid, in Draft draft, PlayfieldView view, double now, double beat, bool editor, bool selected)
    {
        float scale = view.Scale;
        Vector2 vent = view.PlayfieldToScreen(draft.Position);
        float half = Drafts.Diameter * scale / 2f;
        float height = draft.Height * scale;
        float reach = height * 1.25f;

        // The column: air fading out on its way up, past the hover.
        canvas.DrawGradientRect(rid, new Rect2(vent.X - half, vent.Y - reach, half * 2f, reach),
            DraftColor with { A = 0f }, DraftColor with { A = 0.16f }, horizontal: false);

        // Wisps rising through it, each on its own lane and phase, by song time.
        for (int i = 0; i < DraftWisps; i++)
        {
            double phase = now / 900d + i * 0.618d;
            float p = (float)(phase - Math.Floor(phase));
            float x = vent.X - half + half * 2f * ((i * 0.37f + 0.1f) % 1f);
            float y = vent.Y - reach * p;
            float length = 30f * scale + 40f * scale * (1f - p);
            canvas.DrawLine(rid, new Vector2(x, y), new Vector2(x, y - length), DraftColor with { A = 0.5f * (1f - p) }, MathF.Max(1.5f, 3f * scale));
        }

        // The hover: a short bar where the body is held, bobbing on the beat.
        float hoverY = view.PlayfieldToScreen(new Vector2(0f, Drafts.HoverY(draft, beat))).Y;
        float bar = half * 0.5f;
        canvas.DrawLine(rid, new Vector2(vent.X - bar, hoverY), new Vector2(vent.X + bar, hoverY), DraftColor with { A = editor ? 0.9f : 0.35f }, MathF.Max(1.5f, 3f * scale));

        // The emitter: a slab with vents.
        var slab = new Rect2(vent.X - half, vent.Y - Drafts.EmitterHeight * scale / 2f, half * 2f, Drafts.EmitterHeight * scale);
        canvas.DrawRect(rid, slab, new Color(DraftColor.R * 0.5f, DraftColor.G * 0.5f, DraftColor.B * 0.6f, 1f));
        for (int i = 1; i < 6; i++)
        {
            float x = slab.Position.X + slab.Size.X * i / 6f;
            canvas.DrawLine(rid, new Vector2(x, slab.Position.Y + 3f), new Vector2(x, slab.End.Y - 3f), DraftColor with { A = 0.8f }, MathF.Max(1f, 2f * scale));
        }

        canvas.DrawBorder(rid, slab, DraftColor, MathF.Max(1f, 2f * scale));
        if (selected)
            canvas.DrawBorder(rid, new Rect2(vent.X - half, vent.Y - height, half * 2f, height).Grow(4f), SelectedNoteColor, 2f);
    }
}

using System;
using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;
using Xanadu.Ui.Systems.Canvas;
using static Xanadu.Model.Playfields.PlayfieldColors;
using static Xanadu.Model.Playfields.PlayfieldMetrics;

namespace Xanadu.Drawers.Playfields;

public static partial class PlayfieldRenderer
{
    /// <summary>How far the shockwave travels past the note's edge, as a share of the note's radius.</summary>
    private const float RingGrowth = 1.6f;

    /// <summary>Share of the pop the white flash at its start lasts.</summary>
    private const float FlashShare = 0.35f;

    /// <summary>How wide the hit bar starts, as a share of the note's diameter.</summary>
    private const float BarWidth = 1.4f;

    /// <summary>The shader that draws the hit bar from the chart.</summary>
    public const string HitBarShader = "Assets/Shaders/hit_bar.slang";

    /// <summary>
    /// Draws a recently hit note where it was: a white flash over the note,
    /// the note's own shape swelling and fading out, a thick shockwave that
    /// races out from its edge and thins as it goes, and, given the
    /// <paramref name="field"/>, a bar of light up and down it through the note.
    /// </summary>
    public static void DrawNotePop(IRectCanvas canvas, Handle rid, Entity entity, Rect2? field = null)
    {
        NotePop pop = entity.GetComponent<NotePop>();
        float t = Math.Clamp(pop.Age / Math.Max(0.01f, pop.Lifetime), 0f, 1f);
        float alpha = NotePopAlpha(t);
        var color = new Color(
            pop.Color.R,
            pop.Color.G,
            pop.Color.B,
            pop.Color.A * alpha);
        float radius = NotePopRadius(pop.Radius, t);

        if (field is { } bounds)
            DrawHitBar(canvas, rid, pop, bounds);

        DrawNoteBody(canvas, rid, pop.Note, pop.Position, radius, color);

        float swell = PopSwell(t);
        canvas.DrawCircleOutline(
            rid,
            pop.Position,
            pop.Radius * (1f + RingGrowth * swell),
            new Color(color.R, color.G, color.B, color.A * 0.8f),
            Math.Max(1f, 7f * (1f - swell)));

        if (t < FlashShare)
        {
            float flash = 1f - t / FlashShare;
            canvas.DrawCircle(
                rid,
                pop.Position,
                pop.Radius * (0.9f + 0.4f * swell),
                new Color(1f, 1f, 1f, 0.75f * flash * flash));
        }
    }

    /// <summary>
    /// The hit bar, drawn by <see cref="HitBarShader"/> from the chart: one
    /// column the height of the field, centred on the note and as wide as the
    /// bar starts, told which note it is for. The shader does the rest.
    /// </summary>
    private static void DrawHitBar(IRectCanvas canvas, Handle rid, NotePop pop, Rect2 field)
    {
        if (pop.ChartIndex < 0)
            return;

        float half = pop.Radius * BarWidth;
        canvas.SelectCommonShader(HitBarShader);
        canvas.NewRun();
        canvas.SetShaderValue("hit_note", new Vector4(pop.ChartIndex, 0, 0, 0), 1);
        canvas.DrawRect(rid, new Rect2(pop.Position.X - half, field.Position.Y, half * 2, field.Size.Y), Colors.White);
        canvas.SelectCommonShader("");
    }

}

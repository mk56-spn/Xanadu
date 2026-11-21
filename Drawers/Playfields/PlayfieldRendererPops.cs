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
        // The rift draws an entrance's release; a grabbed orb carries on as the ridden orb.
        if (pop.Note.IsEntrance || pop.Note.IsOrb)
            return;

        if (pop.Note.IsDash)
        {
            DrawDashPop(canvas, rid, pop, t);
            return;
        }

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
    /// A hit dash: its bar flashes white, thickens and stretches out across the
    /// field from the chord, thinning to nothing, with two sparks racing out
    /// along it from the middle.
    /// </summary>
    private static void DrawDashPop(IRectCanvas canvas, Handle rid, NotePop pop, float t)
    {
        float scale = pop.Radius * 2f / NoteSize;
        float swell = PopSwell(t);
        float fade = 1f - t;
        float width = Xanadu.Model.Playfields.Chords.DashSize.X * scale * (1f + 1.6f * swell);
        float height = MathF.Max(1f, 26f * scale * (1f - swell) + 2f);
        var bar = new Rect2(pop.Position - new Vector2(width, height) / 2f, new Vector2(width, height));
        canvas.DrawRect(rid, bar, new Color(1f, 1f, 1f, 0.85f * fade * fade));

        float reach = width / 2f + 60f * scale * swell;
        float spark = MathF.Max(3f, 10f * scale) * fade;
        canvas.DrawCircle(rid, pop.Position + new Vector2(reach, 0f), spark, new Color(1f, 1f, 1f, fade));
        canvas.DrawCircle(rid, pop.Position - new Vector2(reach, 0f), spark, new Color(1f, 1f, 1f, fade));
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

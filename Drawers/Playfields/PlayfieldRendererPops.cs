using Friflo.Engine.ECS;
using Xanadu.Core.Items;
using Xanadu.Model.Animation;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Playfields;
using Xanadu.Ui.Systems.Canvas;
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
        var color = new Color(pop.Color.R, pop.Color.G, pop.Color.B, pop.Color.A * alpha);
        float radius = NotePopRadius(pop.Radius, t);

        if (field is { } bounds)
            DrawHitBar(canvas, rid, pop, bounds);

        // The body, the shockwave and the flash, as one look sized to hold the shockwave.
        float swell = PopSwell(t);
        float wave = pop.Radius * (1f + RingGrowth * swell);
        float waveWidth = Math.Max(1f, 7f * (1f - swell));
        float flash = t < FlashShare ? 1f - t / FlashShare : 0f;
        Xif look = NoteLooks.Pop;
        NoteLooks.SetColour(look, color);
        NoteLooks.Set(look, "pop_shape", radius, wave, waveWidth, 0.75f * flash * flash);
        NoteLooks.Set(look, "pop_flash", pop.Radius * (0.9f + 0.4f * swell));
        float half = MathF.Max(radius, wave + waveWidth) + 2f;
        DrawPopLook(canvas, rid, look, pop.Position, new Vector2(half, half));
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
        float reach = width / 2f + 60f * scale * swell;
        float spark = MathF.Max(3f, 10f * scale) * fade;
        Xif look = NoteLooks.DashPop;
        NoteLooks.Set(look, "dashpop_shape", width, height, reach, spark);
        NoteLooks.Set(look, "dashpop_state", fade);
        DrawPopLook(canvas, rid, look, pop.Position, new Vector2(MathF.Max(width / 2f, reach + spark) + 2f, MathF.Max(height / 2f, spark) + 2f));
    }

    /// <summary>A pop look, its unit quad stretched to <paramref name="half"/> pixels either way of <paramref name="at"/>.</summary>
    private static void DrawPopLook(IRectCanvas canvas, Handle rid, Xif look, Vector2 at, Vector2 half)
    {
        var placement = new Transform2D(new Vector2(half.X, 0f), new Vector2(0f, half.Y), at);
        XifDrawer.Shared.DrawNode(canvas, rid, look, placement, 1f, XifTint.Plain);
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

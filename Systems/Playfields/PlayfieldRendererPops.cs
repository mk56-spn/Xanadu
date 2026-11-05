using System;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;

namespace Xanadu.Systems.Playfields;

public static partial class PlayfieldRenderer
{
    public const float NotePopLifetime = 0.32f;

    /// <summary>How much bigger a hit pop grows by the end of its life.</summary>
    private const float PopGrowth = 0.6f;

    /// <summary>How far the shockwave travels past the note's edge, as a share of the note's radius.</summary>
    private const float RingGrowth = 1.6f;

    /// <summary>Share of the pop the white flash at its start lasts.</summary>
    private const float FlashShare = 0.35f;

    /// <summary>
    /// Draws a recently hit note where it was: a white flash over the note,
    /// the note's own shape swelling and fading out, and a thick shockwave
    /// that races out from its edge and thins as it goes.
    /// </summary>
    public static void DrawNotePop(IRectCanvas canvas, Rid rid, Entity entity)
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

    /// <summary>The note's radius after <paramref name="t"/> of its pop, 0..1: a fast, soft swell.</summary>
    public static float NotePopRadius(float radius, float t) =>
        radius * (1f + PopGrowth * PopSwell(t));

    /// <summary>How far through its swell a pop is at <paramref name="t"/> of its life, 0..1.</summary>
    public static float PopSwell(float t)
    {
        float clamped = Math.Clamp(t, 0f, 1f);
        return 1f - (1f - clamped) * (1f - clamped);
    }

    /// <summary>Remaining opacity of a pop at <paramref name="t"/> of its life, 0..1.</summary>
    public static float NotePopAlpha(float t) => 1f - Math.Clamp(t, 0f, 1f);
}

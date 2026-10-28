using System;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;

namespace Xanadu.Systems.Playfields;

public static partial class PlayfieldRenderer
{
    public const float NotePopLifetime = 0.22f;

    /// <summary>How much bigger a hit pop grows by the end of its life.</summary>
    private const float PopGrowth = 0.5f;

    /// <summary>How far the shockwave travels past the note's edge, in pixels.</summary>
    private const float RingGrowth = 8f;

    /// <summary>
    /// Draws each recently hit note where it was: the note's own shape swells
    /// and fades out, with a short shockwave leaving its edge.
    /// </summary>
    public static void DrawNotePops(
        IRectCanvas canvas,
        Rid rid,
        EntityStore entities)
    {
        foreach (Entity entity in entities.Query<NotePop>().ToEntityList())
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

            canvas.DrawCircleOutline(
                rid,
                pop.Position,
                radius + RingGrowth * PopSwell(t),
                new Color(color.R, color.G, color.B, color.A * 0.65f),
                2);
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

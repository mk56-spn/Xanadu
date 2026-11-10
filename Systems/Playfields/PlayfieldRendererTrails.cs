using System;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Settings;
using Xanadu.Systems.Canvas;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Playfields;

public static partial class PlayfieldRenderer
{
    public static void DrawDashTrail(IRectCanvas canvas, Handle rid, Entity entity)
    {
        DashTrail trail = entity.GetComponent<DashTrail>();
        float t = Math.Clamp(
            trail.Age / Math.Max(0.01f, trail.Lifetime),
            0f,
            1f);
        Color color = new(0.55f, 0.9f, 1f, (1f - t) * 0.8f);
        canvas.DrawLine(
            rid,
            trail.From,
            trail.To,
            color,
            Math.Max(1f, 6f * (1f - t)));
    }

    public static void DrawHitTrail(IRectCanvas canvas, Handle rid, Entity entity)
    {
        HitTrail trail = entity.GetComponent<HitTrail>();
        float t = Math.Clamp(
            trail.Age / Math.Max(0.01f, trail.Lifetime),
            0f,
            1f);
        float alpha = 1f - t;
        Color color = trail.Color;

        for (int i = 0; i < 5; i++)
        {
            float offset = (i + 1) * 8f;
            float size = 12f * (1f - t * 0.4f);
            canvas.DrawRect(
                rid,
                new Rect2(
                    trail.Position.X - size / 2,
                    trail.Position.Y - offset - size / 2,
                    size,
                    size),
                new Color(
                    color.R,
                    color.G,
                    color.B,
                    alpha * (1f - i / 5f) * 0.35f));
        }

        var badge = new Rect2(
            trail.Position.X - 22,
            trail.Position.Y + 14,
            44,
            24);
        canvas.DrawRect(
            rid,
            badge,
            new Color(color.R, color.G, color.B, alpha * 0.8f));
        canvas.DrawBorder(
            rid,
            badge,
            new Color(1, 1, 1, alpha * 0.8f),
            1);
        canvas.DrawTextStyled(
            rid,
            badge,
            trail.Label,
            new Color(1, 1, 1, alpha),
            12);
    }
}

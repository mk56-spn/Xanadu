using System;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Settings;
using Xanadu.Systems.Canvas;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Playfields;

public static partial class PlayfieldRenderer
{
    public static void DrawDashTrails(
        IRectCanvas canvas,
        Rid rid,
        EntityStore entities)
    {
        foreach (Entity entity in entities.Query<DashTrail>().ToEntityList())
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
    }

    public static void DrawHitTrails(
        IRectCanvas canvas,
        Rid rid,
        EntityStore entities)
    {
        foreach (Entity entity in entities.Query<HitTrail>().ToEntityList())
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
            DrawBorder(
                canvas,
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

    public static void DrawKeyTrail(
        IRectCanvas canvas,
        Rid rid,
        EntityStore entities,
        Rect2 field,
        Playhead playhead)
    {
        KeyTrail? state = null;
        string[]? labels = null;
        foreach (Entity entity in entities.Query<KeyTrail>().ToEntityList())
        {
            state = entity.GetComponent<KeyTrail>();
        }

        foreach (Entity entity in entities.Query<KeyTrailLabels>().ToEntityList())
        {
            labels = entity.GetComponent<KeyTrailLabels>().Labels;
        }

        if (state is null || !state.Value.Visible)
            return;

        const float keySize = 48;
        const float gap = 8;
        const float historyHeight = 180;
        const float historyWindowMs = 1_000f;
        float total = KeybindSettings.Actions.Length * keySize +
                      (KeybindSettings.Actions.Length - 1) * gap;
        Vector2 hudOffset = state.Value.Offset;
        float startX = Math.Max(10, field.Position.X - total - 12) + hudOffset.X;
        float centerY = field.End.Y - 28 + hudOffset.Y;

        float keyBottom = centerY + keySize / 2;
        float historyTop = centerY - keySize / 2 - 4 - historyHeight;
        var trailBox = new Rect2(
            startX - 10,
            historyTop - 8,
            total + 20,
            keyBottom + 8 - (historyTop - 8));

        canvas.DrawRect(
            rid,
            trailBox,
            new Color(0.02f, 0.03f, 0.06f, 0.45f));
        DrawBorder(
            canvas,
            rid,
            trailBox,
            new Color(0.35f, 0.45f, 0.58f, 0.8f),
            1);

        // Released trails rise out of their keycaps as time advances.
        foreach (Entity entity in entities.Query<KeyPressTrail>().ToEntityList())
        {
            KeyPressTrail segment = entity.GetComponent<KeyPressTrail>();
            int index = (int)segment.Action;
            if (index < 0 || index >= KeybindSettings.Actions.Length)
                continue;

            long now = playhead.TimeMs;
            long end = segment.EndMs >= 0
                ? segment.EndMs
                : now;
            long windowStart = now - (long)historyWindowMs;
            if (end < windowStart || segment.StartMs > now)
                continue;

            float x = startX + index * (keySize + gap) + keySize / 2;
            float bottom = centerY - keySize / 2 - 4;
            float startY = bottom -
                           Math.Clamp(
                               (now - Math.Max(segment.StartMs, windowStart)) /
                               historyWindowMs,
                               0f,
                               1f) * historyHeight;
            float endY = bottom -
                         Math.Clamp(
                             (now - Math.Max(end, windowStart)) /
                             historyWindowMs,
                             0f,
                             1f) * historyHeight;
            float top = Math.Min(startY, endY);
            float height = Math.Max(2f, Math.Abs(endY - startY));
            float age = Math.Clamp(
                (playhead.TimeMs - end) / historyWindowMs,
                0f,
                1f);
            Color color = ColorFor(segment.Action);
            float alpha = 0.18f + (1f - age) * 0.62f;

            var segmentBounds = new Rect2(
                x - keySize * 0.28f,
                top,
                keySize * 0.56f,
                height);
            canvas.DrawRect(
                rid,
                segmentBounds,
                new Color(color.R, color.G, color.B, alpha * 0.35f));
            DrawBorder(
                canvas,
                rid,
                segmentBounds,
                new Color(1f, 1f, 1f, alpha * 0.35f),
                1);
            if (height > 5f)
            {
                canvas.DrawRect(
                    rid,
                    new Rect2(
                        x - 3f,
                        top + 2f,
                        6f,
                        height - 4f),
                    new Color(color.R, color.G, color.B, alpha));
            }
        }

        for (int i = 0; i < KeybindSettings.Actions.Length; i++)
        {
            KeybindAction action = KeybindSettings.Actions[i];
            float intensity = Math.Clamp(state.Value.Get(action), 0f, 1f);
            Color color = ColorFor(action);
            var keyBounds = new Rect2(
                startX + i * (keySize + gap),
                centerY - keySize / 2,
                keySize,
                keySize);

            Color fill = intensity > 0f
                ? new Color(
                    color.R,
                    color.G,
                    color.B,
                    0.18f + intensity * 0.45f)
                : new Color(0.03f, 0.04f, 0.08f, 0.82f);

            canvas.DrawRect(rid, keyBounds, fill);
            DrawBorder(canvas, rid, keyBounds, color, 1);
            canvas.DrawTextStyled(
                rid,
                keyBounds,
                labels is null || i >= labels.Length
                    ? KeybindSettings.LabelFor(action)
                    : labels[i],
                color,
                12);
        }
    }
}

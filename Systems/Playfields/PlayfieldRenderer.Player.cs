using System;
using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Writer.Settings;
using Writer.Systems.Canvas;
using Position = Writer.Ecs.Position;

namespace Writer.Systems.Playfields;

public static partial class PlayfieldRenderer
{
    public static void DrawPlayer(
        IRectCanvas canvas,
        Rid rid,
        EntityStore entities,
        PlayfieldView view)
    {
        foreach (Entity entity in entities.Query<Player, Position>().ToEntityList())
        {
            Player player = entity.GetComponent<Player>();
            Vector2 center = entity.GetComponent<Position>().Value;
            Vector2 size = player.Size;
            var bounds = new Rect2(
                center - size / 2,
                size);

            canvas.DrawRect(rid, bounds, PlayerColor);
            DrawBorder(canvas, rid, bounds, QueueColor, 2);
            return;
        }
    }

    public static void DrawMovementQueue(
        IRectCanvas canvas,
        Rid rid,
        EntityStore entities,
        PlayfieldView view,
        Playhead playhead,
        Rect2 field)
    {
        Entity playerEntity = default;
        foreach (Entity candidate in entities.Query<Player, Position>().ToEntityList())
        {
            playerEntity = candidate;
            break;
        }

        if (playerEntity.IsNull)
            return;

        Vector2 playerScreen = playerEntity.GetComponent<Position>().Value;
        const float lookaheadMs = 1_200f;
        const float activeWindowMs = 150f;

        bool anyUpcoming = false;
        foreach (Entity entity in entities.Query<MovementEvent>().ToEntityList())
        {
            if (entity.Tags.Has<MovementEventUsed>())
                continue;

            long diff = entity.GetComponent<MovementEvent>().TimeMs -
                        playhead.TimeMs;

            if (diff < -activeWindowMs || diff > lookaheadMs)
                continue;

            anyUpcoming = true;

            float t = Math.Clamp(diff / lookaheadMs, 0f, 1f);
            float y = Mathf.Lerp(
                playerScreen.Y,
                field.Position.Y + 12,
                t);
            float width = 26;
            float height = 10;
            var marker = new Rect2(
                playerScreen.X - width / 2,
                y - height / 2,
                width,
                height);

            bool active = Math.Abs(diff) <= activeWindowMs;
            canvas.DrawRect(
                rid,
                marker,
                active ? AccentColor : QueueColor);
            DrawBorder(
                canvas,
                rid,
                marker,
                active ? Colors.White : BorderColor,
                1);
        }

        if (!anyUpcoming)
            return;

        canvas.DrawLine(
            rid,
            new Vector2(playerScreen.X, field.Position.Y + 12),
            new Vector2(playerScreen.X, playerScreen.Y),
            new Color(QueueColor.R, QueueColor.G, QueueColor.B, 0.22f),
            2);
    }
}

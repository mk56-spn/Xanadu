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
    public static void DrawTransport(IRectCanvas canvas, Rid rid, Rect2 field, Playhead playhead)
    {
        float barY = field.End.Y + 10;
        var bar = new Rect2(field.Position.X, barY, field.Size.X, 8);
        canvas.DrawRect(rid, bar, PlayfieldBackground);
        DrawBorder(canvas, rid, bar, BorderColor, 1);

        if (playhead.DurationMs <= 0)
            return;

        float progress = Math.Clamp(
            playhead.TimeMs / (float)playhead.DurationMs,
            0f,
            1f);

        canvas.DrawRect(
            rid,
            new Rect2(bar.Position, new Vector2(bar.Size.X * progress, bar.Size.Y)),
            AccentColor);

        float x = bar.Position.X + bar.Size.X * progress;
        canvas.DrawLine(
            rid,
            new Vector2(x, bar.Position.Y - 2),
            new Vector2(x, bar.End.Y + 2),
            Colors.White,
            2);
    }
}

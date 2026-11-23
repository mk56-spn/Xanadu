using System;
using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;
using Xanadu.Ui.Systems.Canvas;
using static Xanadu.Model.Playfields.PlayfieldColors;
using static Xanadu.Model.Playfields.PlayfieldMetrics;
using Position = Xanadu.Core.Position;

namespace Xanadu.Drawers.Playfields;

public static partial class PlayfieldRenderer
{
    public static void DrawTransport(IRectCanvas canvas, Handle rid, Rect2 field, Playhead playhead)
    {
        float barY = field.End.Y + 10;
        var bar = new Rect2(field.Position.X, barY, field.Size.X, 8);
        canvas.DrawRect(rid, bar, PlayfieldBackground);
        canvas.DrawBorder(rid, bar, BorderColor, 1);

        if (playhead.DurationMs <= 0)
            return;

        float progress = Math.Clamp(
            (float)(playhead.ShownMs / playhead.DurationMs),
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

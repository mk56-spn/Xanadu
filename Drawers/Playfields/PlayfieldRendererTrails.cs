using System;
using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;
using Xanadu.Ui.Systems.Canvas;
using Position = Xanadu.Core.Position;
using static Xanadu.Model.Playfields.PlayfieldColors;
using static Xanadu.Model.Playfields.PlayfieldMetrics;

namespace Xanadu.Drawers.Playfields;

public static partial class PlayfieldRenderer
{
    /// <summary>The player's beam: a bright line into the judgement line that thins and fades, with a flash where it strikes.</summary>
    public static void DrawHitTrail(IRectCanvas canvas, Handle rid, Entity entity)
    {
        HitTrail trail = entity.GetComponent<HitTrail>();
        float t = Math.Clamp(trail.Age / Math.Max(0.01f, trail.Lifetime), 0f, 1f);
        float alpha = 1f - t;
        Color color = trail.Color;
        canvas.DrawLine(rid, trail.From, trail.Position, new Color(color.R, color.G, color.B, alpha * 0.35f), 14f * (1f - t * 0.6f));
        canvas.DrawLine(rid, trail.From, trail.Position, new Color(1f, 1f, 1f, alpha), 4f * (1f - t * 0.5f));
        float flash = 10f + 26f * t;
        canvas.DrawRect(rid, new Rect2(trail.Position - new Vector2(flash, 3f), new Vector2(flash * 2f, 6f)), new Color(color.R, color.G, color.B, alpha));
    }
}

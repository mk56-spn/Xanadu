using System;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Xanadu.Theme;

namespace Xanadu.Systems.Playfields;

/// <summary>Draws spike tiles in both editor and play modes.</summary>
public static partial class PlayfieldRenderer
{
    public static readonly Color SpikeColor = Palette.DangerBright;

    /// <summary>
    /// Draws every spike as one big triangle filling its tile, tip at the middle
    /// of the edge it points at. The selected one, if any, takes the accent colour.
    /// </summary>
    public static void DrawSpikes(
        IRectCanvas canvas,
        Rid rid,
        EntityStore entities,
        PlayfieldView view,
        Entity selected = default)
    {
        Span<Vector2> points = stackalloc Vector2[3];
        foreach (Entity entity in entities.Query<Spike>().ToEntityList())
        {
            Spike spike = entity.GetComponent<Spike>();
            if (spike.Size.X <= 0 || spike.Size.Y <= 0)
                continue;

            canvas.SelectShader(ItemShader.Of(entity));
            SpikeTriangle triangle = SpikeRules.ScreenTriangle(spike, view);
            points[0] = triangle.Tip;
            points[1] = triangle.BaseLeft;
            points[2] = triangle.BaseRight;
            canvas.DrawPolygon(
                rid,
                points,
                entity == selected
                    ? BlockSelectedEdge
                    : GroupColor.For(entity, SpikeColor));
        }

        canvas.SelectShader(0);
    }
}

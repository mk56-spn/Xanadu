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
    public static readonly Color SpikeFill = Palette.DangerSurface;
    public static readonly Color SpikeEdge = Palette.DangerBright;

    /// <summary>Teeth drawn across one tile.</summary>
    private const int SpikeTeeth = 3;

    /// <summary>
    /// Draws every spike as a dark tile with teeth rising from its bottom edge.
    /// The selected one, if any, gets an accent edge.
    /// </summary>
    public static void DrawSpikes(
        IRectCanvas canvas,
        Rid rid,
        EntityStore entities,
        PlayfieldView view,
        Entity selected = default)
    {
        foreach (Entity entity in entities.Query<Spike>().ToEntityList())
        {
            Spike spike = entity.GetComponent<Spike>();
            if (spike.Size.X <= 0 || spike.Size.Y <= 0)
                continue;

            canvas.SelectShader(ItemShader.Of(entity));
            Rect2 screen = new(
                view.PlayfieldToScreen(spike.Position),
                spike.Size * view.Scale);
            Color edge = entity == selected
                ? BlockSelectedEdge
                : GroupColor.For(entity, SpikeEdge);
            Color fill = GroupColor.For(entity, SpikeFill);

            canvas.DrawRect(rid, screen, fill);
            DrawBorder(canvas, rid, screen, edge, 1.5f);
            DrawSpikeTeeth(canvas, rid, screen, edge);
        }

        canvas.SelectShader(0);
    }

    private static void DrawSpikeTeeth(
        IRectCanvas canvas,
        Rid rid,
        Rect2 bounds,
        Color color)
    {
        float width = bounds.Size.X / SpikeTeeth;
        if (width <= 0 || bounds.Size.Y <= 0)
            return;

        Span<Vector2> tooth = stackalloc Vector2[3];
        for (int i = 0; i < SpikeTeeth; i++)
        {
            float left = bounds.Position.X + i * width;
            tooth[0] = new Vector2(left, bounds.End.Y);
            tooth[1] = new Vector2(left + width / 2, bounds.Position.Y);
            tooth[2] = new Vector2(left + width, bounds.End.Y);
            canvas.DrawPolygon(rid, tooth, color);
        }
    }
}

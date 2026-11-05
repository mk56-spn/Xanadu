using System;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;

namespace Xanadu.Systems.Playfields;

/// <summary>Draws spike tiles in both editor and play modes.</summary>
public static partial class PlayfieldRenderer
{
    /// <summary>Spikes have no colour of their own: white shows exactly what their self modulate says.</summary>
    public static readonly Color SpikeColor = Colors.White;

    /// <summary>
    /// Draws one spike as one big triangle filling its box, turned around the
    /// box's centre, inside its outline. A selected one gets an accent edge.
    /// </summary>
    public static void DrawSpike(
        IRectCanvas canvas,
        Rid rid,
        Entity entity,
        PlayfieldView view,
        bool selected)
    {
        Spike spike = entity.GetComponent<Spike>();
        if (spike.Size.X <= 0 || spike.Size.Y <= 0)
            return;

        SelectItemShader(canvas, entity);
        SpikeTriangle triangle = SpikeRules.ScreenTriangle(spike, view);
        DrawSpikeBody(
            canvas,
            rid,
            triangle,
            spike.OutlineWidth * view.Scale,
            GroupColor.For(entity, SpikeColor),
            GroupOutlineColor.For(entity, spike));

        // Like a block, a selected one keeps its own look and gains an accent edge.
        if (selected)
        {
            Span<Vector2> edge = [triangle.Tip, triangle.BaseLeft, triangle.BaseRight, triangle.Tip];
            canvas.DrawPolyline(rid, edge, BlockSelectedEdge, SelectedEdgeThickness);
        }

        canvas.SelectShader(0);
    }

    /// <summary>
    /// A spike's interior and outline, side by side rather than overlapping, as
    /// with a block: the interior is the triangle pulled in by the outline's
    /// width, and the outline is the three strips between the two.
    /// </summary>
    public static void DrawSpikeBody(
        IRectCanvas canvas,
        Rid rid,
        SpikeTriangle triangle,
        float outlineWidth,
        Color fill,
        Color outline)
    {
        Span<Vector2> outer = [triangle.Tip, triangle.BaseLeft, triangle.BaseRight];
        float a = outer[1].DistanceTo(outer[2]);
        float b = outer[2].DistanceTo(outer[0]);
        float c = outer[0].DistanceTo(outer[1]);
        float perimeter = a + b + c;
        float area = Mathf.Abs((outer[1] - outer[0]).Cross(outer[2] - outer[0])) / 2f;
        if (perimeter <= 0f || area <= 0f)
            return;

        // Pulling every edge in by the same width scales the triangle about its incentre.
        Vector2 incentre = (outer[0] * a + outer[1] * b + outer[2] * c) / perimeter;
        float inradius = 2f * area / perimeter;
        float keep = outlineWidth > 0f ? Mathf.Max(0f, 1f - outlineWidth / inradius) : 1f;
        Span<Vector2> inner = stackalloc Vector2[3];
        for (int i = 0; i < 3; i++)
            inner[i] = incentre + (outer[i] - incentre) * keep;

        if (keep > 0f)
            canvas.DrawPolygon(rid, inner, fill);
        if (keep >= 1f)
            return;

        Span<Vector2> strip = stackalloc Vector2[4];
        for (int i = 0; i < 3; i++)
        {
            int next = (i + 1) % 3;
            strip[0] = outer[i];
            strip[1] = outer[next];
            strip[2] = inner[next];
            strip[3] = inner[i];
            canvas.DrawPolygon(rid, strip, outline);
        }
    }
}

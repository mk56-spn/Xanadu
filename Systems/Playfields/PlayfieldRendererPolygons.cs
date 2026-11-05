using System;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Items;
using Xanadu.Systems.Canvas;

namespace Xanadu.Systems.Playfields;

/// <summary>Draws polygon items, in both editor and play modes.</summary>
public static partial class PlayfieldRenderer
{
    /// <summary>A new polygon's fill.</summary>
    public static readonly Color PolygonFill = new("#7aa2f7");

    /// <summary>
    /// Fills one polygon item, each primitive in its own colour (self modulate
    /// and group fades applied) and with its own shader, else the item's. A
    /// selected item gets its outlines traced.
    /// </summary>
    public static void DrawPolygon(
        IRectCanvas canvas,
        Rid rid,
        Entity entity,
        PlayfieldView view,
        bool selected)
    {
        LevelPolygon polygon = entity.GetComponent<LevelPolygon>();
        if (polygon.Item is null)
            return;

        bool followsGroup = SelectItemShader(canvas, entity);
        foreach (XifPrimitive primitive in polygon.Item.Primitives)
        {
            if (!primitive.IsPolygon || primitive.Points.Count < 3)
                continue;

            Vector2[] outline = ScreenOutline(polygon, primitive, view);
            if (!followsGroup)
                SelectPrimitiveShader(canvas, primitive, ItemShader.Of(entity), ItemBlend.Of(entity));
            canvas.DrawPolygon(rid, outline, GroupColor.For(entity, primitive.Fill));
            if (!followsGroup)
            {
                canvas.SelectCommonShader("");
                canvas.SelectShader(0);
            }

            if (selected)
                DrawOutline(canvas, rid, outline, BlockSelectedEdge);
        }

        if (followsGroup)
            canvas.SelectShader(0);
    }

    /// <summary>A primitive's outline on screen.</summary>
    public static Vector2[] ScreenOutline(LevelPolygon polygon, XifPrimitive primitive, PlayfieldView view)
    {
        Vector2[] outline = polygon.Outline(primitive);
        for (int i = 0; i < outline.Length; i++)
            outline[i] = view.PlayfieldToScreen(outline[i]);
        return outline;
    }

    /// <summary>A closed outline, traced.</summary>
    public static void DrawOutline(IRectCanvas canvas, Rid rid, ReadOnlySpan<Vector2> outline, Color color)
    {
        if (outline.Length < 2)
            return;

        var closed = new Vector2[outline.Length + 1];
        outline.CopyTo(closed);
        closed[^1] = outline[0];
        canvas.DrawPolyline(rid, closed, color, SelectedEdgeThickness);
    }

    private static void SelectPrimitiveShader(
        IRectCanvas canvas,
        XifPrimitive primitive,
        int itemSlot,
        Xanadu.Rendering.BlendMode blend)
    {
        if (primitive.Shader.Scope == XifShaderScope.Common && primitive.Shader.Path.Length > 0)
            canvas.SelectCommonShader(primitive.Shader.Path, blend);
        else
            canvas.SelectShader(primitive.Shader.IsNone ? itemSlot : primitive.Shader.Slot, blend);
    }

    /// <summary>The upright box around a polygon item, on screen.</summary>
    public static Rect2 PolygonScreenBounds(LevelPolygon polygon, PlayfieldView view)
    {
        Rect2 bounds = polygon.Bounds;
        return new Rect2(view.PlayfieldToScreen(bounds.Position), bounds.Size * view.Scale);
    }
}

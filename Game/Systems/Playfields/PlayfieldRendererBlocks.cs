using System;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Xanadu.Theme;

namespace Xanadu.Systems.Playfields;

/// <summary>Draws collidable blocks in both editor and play modes.</summary>
public static partial class PlayfieldRenderer
{
    public static readonly Color BlockFill = Colors.White;

    /// <summary>The dark rim of the editor's handles, which are not part of any block.</summary>
    public static readonly Color HandleEdge = Palette.BorderStrong;
    public static readonly Color BlockSelectedEdge = Palette.Accent;

    /// <summary>A one-way block is see-through, so it reads as something to pass through.</summary>
    private const float OneWayFillAlpha = 0.45f;

    private const float OneWayEdgeThickness = 6f;

    private const float SelectedEdgeThickness = 2f;

    /// <summary>The strip along the one side of a block that collides.</summary>
    public static Rect2 OneWayEdge(Rect2 screen, BlockOneWay side)
    {
        float width = Mathf.Min(OneWayEdgeThickness, screen.Size.X);
        float height = Mathf.Min(OneWayEdgeThickness, screen.Size.Y);
        return side switch
        {
            BlockOneWay.Up => new Rect2(screen.Position, screen.Size.X, height),
            BlockOneWay.Right => new Rect2(screen.End.X - width, screen.Position.Y, width, screen.Size.Y),
            BlockOneWay.Down => new Rect2(screen.Position.X, screen.End.Y - height, screen.Size.X, height),
            BlockOneWay.Left => new Rect2(screen.Position, width, screen.Size.Y),
            _ => screen,
        };
    }

    /// <summary>
    /// A block's interior and outline, side by side rather than overlapping so a
    /// see-through interior does not show the outline's colour through it. The
    /// strip marking a one-way block's solid side always shows, outline or not.
    /// </summary>
    public static void DrawBlockBody(
        IRectCanvas canvas,
        Handle rid,
        Rect2 screen,
        float outlineWidth,
        Color fill,
        Color outline,
        BlockOneWay oneWay = BlockOneWay.Off)
    {
        Color strip = outline;
        if (oneWay != BlockOneWay.Off)
        {
            fill.A *= OneWayFillAlpha;
            outline.A *= OneWayFillAlpha;
        }

        float border = Mathf.Clamp(outlineWidth, 0f, Mathf.Min(screen.Size.X, screen.Size.Y) / 2);
        Rect2 interior = border > 0f ? screen.Grow(-border) : screen;
        if (interior.Size.X > 0 && interior.Size.Y > 0)
            canvas.DrawRect(rid, interior, fill);
        if (border > 0f)
            canvas.DrawBorder(rid, screen, outline, border);
        if (oneWay != BlockOneWay.Off)
            canvas.DrawRect(rid, OneWayEdge(screen, oneWay), strip);
    }

    /// <summary>A ramp: the half of its box under the diagonal, filled, with its outline round all three edges.</summary>
    private static void DrawRamp(IRectCanvas canvas, Handle rid, Rect2 screen, Block block, float scale, Color fill, Color outline)
    {
        Vector2[] corners = Block.RampCorners(screen.Position, screen.Size, block.Slope);
        canvas.DrawPolygon(rid, corners, fill);
        float width = Mathf.Max(block.OutlineWidth * scale, 1f);
        for (int i = 0; i < corners.Length; i++)
            canvas.DrawLine(rid, corners[i], corners[(i + 1) % corners.Length], outline, width);
    }

    /// <summary>Whether any playfield in the store is playing rather than being edited.</summary>
    public static bool IsPlaying(EntityStore entities)
    {
        foreach (var (modes, _) in entities.Cached<PlayfieldMode>().Chunks)
        {
            foreach (PlayfieldMode mode in modes.Span)
            {
                if (mode.Kind == PlayfieldKind.Play)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Draws one block as a filled slab inside its outline. A selected one gets
    /// an accent edge even with no outline of its own; the editor draws the
    /// primary one's resize handles on top.
    /// </summary>
    public static void DrawBlock(
        IRectCanvas canvas,
        Handle rid,
        Entity entity,
        PlayfieldView view,
        bool selected,
        bool playing)
    {
        Block block = entity.GetComponent<Block>();
        if (block.Size.X <= 0 || block.Size.Y <= 0)
            return;

        SelectItemShader(canvas, entity);
        Rect2 screen = new(
            view.PlayfieldToScreen(block.Position),
            block.Size * view.Scale);
        Color outline = GroupOutlineColor.For(entity, block);
        Color fill = GroupColor.For(entity, BlockFill);
        if (block.Slope != BlockSlope.Off)
        {
            DrawRamp(canvas, rid, screen, block, view.Scale, fill, selected ? BlockSelectedEdge : outline);
            canvas.SelectShader(0);
            return;
        }

        DrawBlockBody(canvas, rid, screen, block.OutlineWidth * view.Scale, fill, outline, block.OneWay);

        Color edge = outline;
        if (selected)
        {
            edge = BlockSelectedEdge;
            canvas.DrawBorder(rid, screen, edge, SelectedEdgeThickness);
        }


        canvas.SelectShader(0);
    }
}

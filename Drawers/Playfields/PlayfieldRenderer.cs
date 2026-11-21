using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Model.Ecs.Components;
using Xanadu.Ui.Systems.Canvas;
using Color = Xanadu.Core.Maths.Color;
using Position = Xanadu.Core.Position;
using Rect2 = Xanadu.Core.Maths.Rect2;
using static Xanadu.Model.Playfields.PlayfieldColors;
using static Xanadu.Model.Playfields.PlayfieldMetrics;
using Xanadu.Model.Playfields;

namespace Xanadu.Drawers.Playfields;

/// <summary>Shared playfield geometry and drawing used by editor and play modes.</summary>
public static partial class PlayfieldRenderer
{
    public static void DrawPlay(
        IRectCanvas canvas,
        Handle rid,
        EntityStore entities,
        PlayfieldView view,
        Playfield playfield,
        Playhead playhead,
        Vector2 camera = default,
        Rect2? frame = null,
        float seconds = 0f)
    {
        // Under a camera the layer is scrolled by it, so the backdrop follows to keep the screen covered.
        canvas.DrawRect(rid, new Rect2(camera, view.ScreenSize), PlayBackground);

        Rect2 field = FieldRect(view, playfield);
        DrawFieldFrame(canvas, rid, field, view.Scale, null);
        canvas.DrawRect(rid, field, PlayfieldBackground);
        canvas.DrawBorder(rid, field, BorderColor, 2);

        DrawItems(
            canvas,
            rid,
            entities,
            new PlayfieldItemFrame(view, playfield, playhead, seconds, Editor: false),
            () =>
            {
                DrawEntrance(canvas, rid, entities, view, playfield, playhead);
                DrawRiddenOrb(canvas, rid, entities, view);
                DrawPlayer(canvas, rid, entities, view);
            });
        DrawTransport(canvas, rid, frame ?? field, playhead);
    }

    /// <summary>The frame's width in playfield units; the view scales it, within <see cref="FrameMinPixels"/> and <see cref="FrameMaxPixels"/>.</summary>
    public const float FrameWidth = 12f;

    public const float FrameMinPixels = 3f;

    public const float FrameMaxPixels = 12f;

    /// <summary>The frame's width on screen for a playfield drawn at <paramref name="scale"/>.</summary>
    public static float FramePixels(float scale) =>
        Math.Clamp(FrameWidth * scale, FrameMinPixels, FrameMaxPixels);

    /// <summary>
    /// A small frame just outside the field, so its edge reads against whatever
    /// colour surrounds it, in the level's frame colour if it sets one. The field
    /// and its border line are drawn over its inner edge.
    /// </summary>
    public static void DrawFieldFrame(IRectCanvas canvas, Handle rid, Rect2 field, float scale, Color? color = null)
    {
        float width = FramePixels(scale);
        canvas.DrawBorder(rid, field.Grow(width), color ?? FrameColor, width);
    }

    public static void DrawField(
        IRectCanvas canvas,
        Handle rid,
        Rect2 field,
        float borderWidth = 1,
        Color? background = null,
        Color? border = null)
    {
        canvas.DrawRect(rid, field, background ?? PlayfieldBackground);
        canvas.DrawBorder(rid, field, border ?? BorderColor, borderWidth);
    }

    /// <summary>Thinnest a drawn grid cell gets on screen; past that every second line is dropped.</summary>
    private const float MinGridCell = 10f;

    /// <summary>
    /// Draws the field's grid, only where <paramref name="visible"/> shows it,
    /// and the field's size along its bottom edge.
    /// </summary>
    public static void DrawGrid(
        IRectCanvas canvas,
        Handle rid,
        Playfield playfield,
        Rect2 field,
        Rect2? visible = null)
    {
        Rect2 bounds = visible is { } area ? field.Intersection(area) : field;
        if (bounds.Size.X > 0 && bounds.Size.Y > 0)
            CanvasGrid.Draw(canvas, rid, bounds, DrawnCell(playfield, field), field.Position + field.Size / 2);
    }

    /// <summary>The field's width marks along its bottom edge, which stay with the grid off.</summary>
    public static void DrawFieldLabels(
        IRectCanvas canvas,
        Handle rid,
        Playfield playfield,
        Rect2 field)
    {
        float labelY = field.End.Y - 20;
        DrawLeftText(canvas, rid, "0", field.Position.X + 6, labelY, DimTextColor, field.Size.X);
        DrawLeftText(
            canvas,
            rid,
            (playfield.Width / 2).ToString(),
            field.Position.X + field.Size.X / 2 - 18,
            labelY,
            DimTextColor,
            field.Size.X);
        DrawLeftText(
            canvas,
            rid,
            playfield.Width.ToString(),
            field.End.X - 46,
            labelY,
            DimTextColor,
            field.Size.X);
    }

    /// <summary>
    /// Carries the field's grid on past its edges across <paramref name="area"/>,
    /// fainter, so the grid does not stop dead at the border. Drawn before the
    /// field, which covers it inside.
    /// </summary>
    public static void DrawOuterGrid(
        IRectCanvas canvas,
        Handle rid,
        Playfield playfield,
        Rect2 field,
        Rect2 area)
    {
        CanvasGrid.Draw(
            canvas,
            rid,
            area,
            DrawnCell(playfield, field),
            field.Position + field.Size / 2,
            new Color(GridColor, GridColor.A * 0.45f),
            new Color(GridColor, GridColor.A * 0.45f));
    }

    /// <summary>The grid's cell on screen, doubled until it is not too dense to read.</summary>
    private static Vector2 DrawnCell(Playfield playfield, Rect2 field)
    {
        Vector2 cell = PlayfieldGrid.Cell(playfield) * (field.Size.X / Math.Max(1, playfield.Width));
        while (cell.X > 0 && cell.X < MinGridCell)
            cell *= 2;
        return cell;
    }

    private static void DrawLeftText(
        IRectCanvas canvas,
        Handle rid,
        string text,
        float x,
        float y,
        Color color,
        float width) =>
        canvas.DrawTextLeft(
            rid,
            new Rect2(x, y, width - 32, 20),
            text,
            color);
}

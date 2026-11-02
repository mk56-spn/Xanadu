using System;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Settings;
using Xanadu.Systems.Canvas;
using Xanadu.Theme;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Playfields;

/// <summary>Shared playfield geometry and drawing used by editor and play modes.</summary>
public static partial class PlayfieldRenderer
{
    public static readonly Color PlayBackground = Palette.Stage;
    public static readonly Color PlayfieldBackground = Palette.Stage;
    public static readonly Color BorderColor = Palette.BorderStrong;
    public static readonly Color GridColor = CanvasGrid.LineColor;
    public static readonly Color AxisColor = CanvasGrid.AxisColor;
    public static readonly Color AccentColor = Palette.Accent;
    public static readonly Color DimTextColor = Palette.TextDim;
    public static readonly Color NoteColor = new(1f, 0.55f, 0.15f);
    public static readonly Color SelectedNoteColor = new(1f, 0.9f, 0.3f);
    public static readonly Color PlayerColor = Palette.Text;
    public static readonly Color QueueColor = Palette.Info;
    public static readonly Color CharacterSwitchColor = Palette.Lime;
    public static readonly Color PairColor = Palette.TextBright;
    public static readonly Color ContactBarrierColor = Palette.Violet;
    public static readonly Color WeaponSwitchColor = Palette.Success;
    public static readonly Color ChannelColor = Palette.Selected;
    public static readonly Color RailColor = Palette.InfoSoft;
    public static readonly Color ConstructionColor = Palette.Primary;
    public static readonly Color ConstructionStepColor = Palette.Accent;
    public static readonly Color GuideColor = Palette.Text;
    public static readonly Color StartPointColor = Palette.SuccessBright;

    public const float MineSize = 18;

    private static readonly Color[] LaneColors =
    {
        Palette.Cyan,
        Palette.PrimarySoft,
        Palette.Violet,
        Palette.Accent,
        Palette.Warning,
        Palette.Danger,
        Palette.Pink,
    };

    public const float NoteSize = 72;

    public static Rect2 FieldRect(PlayfieldView view, Playfield playfield) =>
        new(
            view.ScreenOrigin,
            new Vector2(playfield.Width * view.Scale, playfield.Height * view.Scale));

    public static void DrawPlay(
        IRectCanvas canvas,
        Rid rid,
        EntityStore entities,
        PlayfieldView view,
        Playfield playfield,
        Playhead playhead,
        int fieldShader = 0,
        int playerShader = 0,
        Color? background = null,
        Vector2 camera = default,
        Rect2? frame = null)
    {
        // Under a camera the layer is scrolled by it, so the backdrop follows to keep the screen covered.
        canvas.DrawRect(rid, new Rect2(camera, view.ScreenSize), PlayBackground);

        Rect2 field = FieldRect(view, playfield);
        canvas.SelectShader(fieldShader);
        canvas.DrawRect(rid, field, background ?? PlayfieldBackground);
        canvas.DrawBorder(rid, field, BorderColor, 2);
        canvas.SelectShader(0);

        DrawBlocks(canvas, rid, entities, view);
        DrawSpikes(canvas, rid, entities, view);
        DrawNotes(canvas, rid, entities, view, playfield, playhead, default);
        DrawRails(canvas, rid, entities, view, playfield, playhead, previewBoth: false);
        DrawNotePops(canvas, rid, entities);
        DrawMines(canvas, rid, entities, view, playhead);
        DrawDashTrails(canvas, rid, entities);
        canvas.SelectShader(playerShader);
        DrawPlayer(canvas, rid, entities, view);
        DrawSplitlineBalls(canvas, rid, entities, view);
        canvas.SelectShader(0);
        DrawHitTrails(canvas, rid, entities);
        DrawKeyTrail(canvas, rid, entities, frame ?? field, playhead, camera.X);
        DrawTransport(canvas, rid, frame ?? field, playhead);
    }

    public static void DrawField(
        IRectCanvas canvas,
        Rid rid,
        Rect2 field,
        float borderWidth = 1,
        Color? background = null)
    {
        canvas.DrawRect(rid, field, background ?? PlayfieldBackground);
        canvas.DrawBorder(rid, field, BorderColor, borderWidth);
    }

    /// <summary>Thinnest a drawn grid cell gets on screen; past that every second line is dropped.</summary>
    private const float MinGridCell = 10f;

    /// <summary>
    /// Draws the field's grid, only where <paramref name="visible"/> shows it,
    /// and the field's size along its bottom edge.
    /// </summary>
    public static void DrawGrid(
        IRectCanvas canvas,
        Rid rid,
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
        Rid rid,
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
        Rid rid,
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
        Rid rid,
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

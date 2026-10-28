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
        int playerShader = 0)
    {
        canvas.DrawRect(rid, new Rect2(Vector2.Zero, view.ScreenSize), PlayBackground);

        Rect2 field = FieldRect(view, playfield);
        canvas.SelectShader(fieldShader);
        canvas.DrawRect(rid, field, PlayfieldBackground);
        DrawBorder(canvas, rid, field, BorderColor, 2);
        canvas.SelectShader(0);

        DrawBlocks(canvas, rid, entities, view);
        DrawNotes(canvas, rid, entities, view, playfield, playhead, default);
        DrawMines(canvas, rid, entities, view, playhead);
        DrawDashTrails(canvas, rid, entities);
        canvas.SelectShader(playerShader);
        DrawPlayer(canvas, rid, entities, view);
        canvas.SelectShader(0);
        DrawHitTrails(canvas, rid, entities);
        DrawKeyTrail(canvas, rid, entities, field, playhead);
        DrawTransport(canvas, rid, field, playhead);
    }

    public static void DrawField(
        IRectCanvas canvas,
        Rid rid,
        Rect2 field,
        float borderWidth = 1)
    {
        canvas.DrawRect(rid, field, PlayfieldBackground);
        DrawBorder(canvas, rid, field, BorderColor, borderWidth);
    }

    public static void DrawGrid(
        IRectCanvas canvas,
        Rid rid,
        Playfield playfield,
        Rect2 field)
    {
        const int divisions = 10;

        CanvasGrid.Draw(
            canvas,
            rid,
            field,
            field.Size / divisions,
            field.Position + field.Size / 2);

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









    public static void DrawBorder(
        IRectCanvas canvas,
        Rid rid,
        Rect2 bounds,
        Color color,
        float width)
    {
        canvas.DrawRect(rid,
            new Rect2(bounds.Position, new Vector2(bounds.Size.X, width)), color);
        canvas.DrawRect(rid,
            new Rect2(new Vector2(bounds.Position.X, bounds.End.Y - width),
                new Vector2(bounds.Size.X, width)), color);
        canvas.DrawRect(rid,
            new Rect2(bounds.Position, new Vector2(width, bounds.Size.Y)), color);
        canvas.DrawRect(rid,
            new Rect2(new Vector2(bounds.End.X - width, bounds.Position.Y),
                new Vector2(width, bounds.Size.Y)), color);
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

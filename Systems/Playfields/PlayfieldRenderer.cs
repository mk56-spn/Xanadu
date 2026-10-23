using System;
using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Writer.Settings;
using Writer.Systems.Canvas;
using Position = Writer.Ecs.Position;

namespace Writer.Systems.Playfields;

/// <summary>Shared playfield geometry and drawing used by editor and play modes.</summary>
public static partial class PlayfieldRenderer
{
    public static readonly Color PlayBackground = new("#090d16");
    public static readonly Color PlayfieldBackground = new("#0f172a");
    public static readonly Color BorderColor = new("#475569");
    public static readonly Color GridColor = new("#273449");
    public static readonly Color AxisColor = new("#64748b");
    public static readonly Color AccentColor = new("#facc15");
    public static readonly Color DimTextColor = new("#94a3b8");
    public static readonly Color NoteColor = new(1f, 0.55f, 0.15f);
    public static readonly Color SelectedNoteColor = new(1f, 0.9f, 0.3f);
    public static readonly Color PlayerColor = new("#e2e8f0");
    public static readonly Color QueueColor = new("#38bdf8");

    public const float MineSize = 18;

    private static readonly Color[] LaneColors =
    {
        new("#22d3ee"),
        new("#3b82f6"),
        new("#8b5cf6"),
        new("#facc15"),
        new("#fb923c"),
        new("#ef4444"),
        new("#ec4899"),
    };

    public const float NoteSize = 24;

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
        Playhead playhead)
    {
        canvas.DrawRect(rid, new Rect2(Vector2.Zero, view.ScreenSize), PlayBackground);

        Rect2 field = FieldRect(view, playfield);
        canvas.DrawRect(rid, field, PlayfieldBackground);
        DrawBorder(canvas, rid, field, BorderColor, 2);

        DrawNotes(canvas, rid, entities, view, playfield, playhead, default);
        DrawMines(canvas, rid, entities, view, playhead);
        DrawDashTrails(canvas, rid, entities);
        DrawPlayer(canvas, rid, entities, view);
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

        for (int i = 0; i <= divisions; i++)
        {
            float t = i / (float)divisions;
            float x = field.Position.X + field.Size.X * t;
            float y = field.Position.Y + field.Size.Y * t;
            bool center = i == divisions / 2;
            Color color = center ? AxisColor : GridColor;
            float width = center ? 2 : 1;

            canvas.DrawLine(
                rid,
                new Vector2(x, field.Position.Y),
                new Vector2(x, field.End.Y),
                color,
                width);

            canvas.DrawLine(
                rid,
                new Vector2(field.Position.X, y),
                new Vector2(field.End.X, y),
                color,
                width);
        }

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

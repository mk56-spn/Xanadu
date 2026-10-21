using System;
using System.IO;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Writer.Systems.Canvas;

namespace Writer.Systems.Editor;

/// <summary>Draws the scaled 1000x1000 editor playfield and inspector panel.</summary>
public sealed class EditorPlayfieldDrawSystem : QuerySystem<EditorView, LevelInfo, Playfield, EditorPlayhead>
{
    private const float Margin = 30;
    private const float PanelGap = 30;

    private static readonly Color EditorBackground = new("#1e1e1e");
    private static readonly Color PlayfieldBackground = new("#0f172a");
    private static readonly Color PanelBackground = new("#111827");
    private static readonly Color BorderColor = new("#475569");
    private static readonly Color GridColor = new("#273449");
    private static readonly Color AxisColor = new("#64748b");
    private static readonly Color TitleColor = new("#facc15");
    private static readonly Color TextColor = new("#cbd5e1");
    private static readonly Color DimTextColor = new("#94a3b8");

    private readonly IRectCanvas _canvas;
    private readonly Rid _editor;

    public EditorPlayfieldDrawSystem(IRectCanvas canvas, Rid editor)
    {
        _canvas = canvas;
        _editor = editor;
    }

    protected override void OnUpdate()
    {
        _canvas.Clear(_editor);

        Query.ForEachEntity((ref view, ref level, ref playfield, ref playhead, _) =>
        {
            DrawEditor(view, level, playfield, playhead);
        });
    }

    private void DrawEditor(
        EditorView view,
        LevelInfo level,
        Playfield playfield,
        EditorPlayhead playhead)
    {
        _canvas.DrawRect(
            _editor,
            new Rect2(Vector2.Zero, view.ScreenSize),
            EditorBackground);

        float fieldWidth = playfield.Width * view.Scale;
        float fieldHeight = playfield.Height * view.Scale;
        var field = new Rect2(view.ScreenOrigin, new Vector2(fieldWidth, fieldHeight));

        _canvas.DrawRect(_editor, field, PlayfieldBackground);
        DrawBorder(field, BorderColor, 1);

        DrawGrid(view, playfield, field);

        float panelX = field.End.X + PanelGap;
        float panelWidth = view.ScreenSize.X - panelX - Margin;
        var panel = new Rect2(panelX, field.Position.Y, panelWidth, field.Size.Y);
        _canvas.DrawRect(_editor, panel, PanelBackground);
        DrawBorder(panel, BorderColor, 1);

        float x = panel.Position.X + 16;
        float y = panel.Position.Y + 18;

        DrawPanelText("EDITOR", x, y, TitleColor, panel.Size.X);
        y += 34;

        DrawPanelText($"Level: {level.Name}", x, y, TextColor, panel.Size.X);
        y += 24;

        string song = string.IsNullOrEmpty(level.AudioPath)
            ? "not selected"
            : Path.GetFileName(level.AudioPath);
        DrawPanelText($"Song: {song}", x, y, TextColor, panel.Size.X);
        y += 24;

        DrawPanelText(
            $"Playfield {playfield.Width} x {playfield.Height}",
            x,
            y,
            TextColor,
            panel.Size.X);
        y += 24;

        DrawPanelText(
            $"Time {FormatMs(playhead.TimeMs)} / {FormatMs(playhead.DurationMs)}",
            x,
            y,
            TextColor,
            panel.Size.X);
        y += 24;

        DrawPanelText(
            playhead.Playing ? "playing" : "paused",
            x,
            y,
            DimTextColor,
            panel.Size.X);

        DrawTransport(field, playhead);
    }

    private void DrawTransport(Rect2 field, EditorPlayhead playhead)
    {
        float barY = field.End.Y + 10;
        var bar = new Rect2(field.Position.X, barY, field.Size.X, 8);
        _canvas.DrawRect(_editor, bar, PlayfieldBackground);
        DrawBorder(bar, BorderColor, 1);

        if (playhead.DurationMs <= 0)
            return;

        float progress = Math.Clamp(
            playhead.TimeMs / (float)playhead.DurationMs,
            0f,
            1f);

        _canvas.DrawRect(
            _editor,
            new Rect2(bar.Position, new Vector2(bar.Size.X * progress, bar.Size.Y)),
            TitleColor);

        float x = bar.Position.X + bar.Size.X * progress;
        _canvas.DrawLine(
            _editor,
            new Vector2(x, bar.Position.Y - 2),
            new Vector2(x, bar.End.Y + 2),
            Colors.White,
            2);
    }

    private void DrawGrid(EditorView view, Playfield playfield, Rect2 field)
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

            _canvas.DrawLine(
                _editor,
                new Vector2(x, field.Position.Y),
                new Vector2(x, field.End.Y),
                color,
                width);

            _canvas.DrawLine(
                _editor,
                new Vector2(field.Position.X, y),
                new Vector2(field.End.X, y),
                color,
                width);
        }

        float labelY = field.End.Y - 20;
        DrawPanelText("0", field.Position.X + 6, labelY, DimTextColor, field.Size.X);
        DrawPanelText(
            (playfield.Width / 2).ToString(),
            field.Position.X + field.Size.X / 2 - 18,
            labelY,
            DimTextColor,
            field.Size.X);
        DrawPanelText(
            playfield.Width.ToString(),
            field.End.X - 46,
            labelY,
            DimTextColor,
            field.Size.X);
    }

    private void DrawPanelText(string text, float x, float y, Color color, float panelWidth) =>
        _canvas.DrawTextLeft(
            _editor,
            new Rect2(x, y, panelWidth - 32, 20),
            text,
            color);

    private void DrawBorder(Rect2 bounds, Color color, float width)
    {
        _canvas.DrawRect(_editor,
            new Rect2(bounds.Position, new Vector2(bounds.Size.X, width)), color);
        _canvas.DrawRect(_editor,
            new Rect2(new Vector2(bounds.Position.X, bounds.End.Y - width),
                new Vector2(bounds.Size.X, width)), color);
        _canvas.DrawRect(_editor,
            new Rect2(bounds.Position, new Vector2(width, bounds.Size.Y)), color);
        _canvas.DrawRect(_editor,
            new Rect2(new Vector2(bounds.End.X - width, bounds.Position.Y),
                new Vector2(width, bounds.Size.Y)), color);
    }

    private static string FormatMs(long milliseconds)
    {
        long value = Math.Max(0, milliseconds);
        return $"{value / 60000:00}:{value / 1000 % 60:00}.{value % 1000:000}";
    }
}

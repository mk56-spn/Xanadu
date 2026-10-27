using System;
using Godot;
using Writer.Ecs;
using Writer.Levels;

namespace Writer.Systems.Editor;

/// <summary>
/// Where each piece of the group overlay sits, in pixels from the dialog's
/// top-left corner, and the time-to-pixel maths of its timeline. Shared by the
/// builder, the system and the tests so they cannot disagree.
/// </summary>
public static class EditorGroupLayout
{
    /// <summary>Effects shown at once; a group can hold more, but only these rows are editable.</summary>
    public const int MaxRows = 8;

    /// <summary>Room the groups grid takes in the sidebar: its label, four rows of cells, the preview toggle and a margin.</summary>
    public const float GridHeight = 20f + 4 * 40f + 6f + 40f;

    /// <summary>How far below the grid's top the preview toggle sits, from centre to centre.</summary>
    public const float PreviewTop = 191f;

    public const float RowHeight = 36f;
    public const float Pad = 20f;
    public const int TickSlots = 96;
    public const int LabelSlots = 9;

    private const float RowsTop = 168f;
    private const float TableWidth = 500f;

    public static Vector2 DialogSize(Vector2 screen) => new(
        Math.Min(1120f, screen.X - 48f),
        Math.Min(576f, screen.Y - 48f));

    /// <summary>The area the timeline's rows share, to the right of the table.</summary>
    public static Rect2 Timeline(Vector2 dialog) => new(
        Pad + TableWidth + 20f,
        RowsTop,
        dialog.X - (Pad + TableWidth + 20f) - Pad,
        MaxRows * RowHeight);

    /// <summary>The strip above the rows with the tick marks and second labels.</summary>
    public static Rect2 Ruler(Vector2 dialog)
    {
        Rect2 timeline = Timeline(dialog);
        return new Rect2(timeline.Position.X, RowsTop - 34f, timeline.Size.X, 30f);
    }

    public static Rect2 RowTrack(Vector2 dialog, int row)
    {
        Rect2 timeline = Timeline(dialog);
        return new Rect2(timeline.Position.X, RowsTop + row * RowHeight + 2f, timeline.Size.X, RowHeight - 4f);
    }

    public static float XForTime(Rect2 timeline, float viewStart, float viewSpan, float seconds) =>
        timeline.Position.X + (seconds - viewStart) / viewSpan * timeline.Size.X;

    public static float TimeForX(Rect2 timeline, float viewStart, float viewSpan, float x) =>
        viewStart + (x - timeline.Position.X) / timeline.Size.X * viewSpan;

    /// <summary>The rectangle of a piece, relative to the dialog's top-left.</summary>
    public static Rect2 Place(EditorGroupPart part, int row, Vector2 dialog)
    {
        float right = dialog.X - Pad;
        float rowTop = RowsTop + row * RowHeight + 4f;
        return part switch
        {
            EditorGroupPart.Scrim => new Rect2(Vector2.Zero, dialog),
            EditorGroupPart.Dialog => new Rect2(Vector2.Zero, dialog),
            EditorGroupPart.Title => new Rect2(Pad, 14, 190, 30),
            EditorGroupPart.NameField => new Rect2(Pad + 200, 14, 280, 30),
            EditorGroupPart.Close => new Rect2(right - 100, 14, 100, 30),
            EditorGroupPart.MemberText => new Rect2(Pad, 58, 190, 28),
            EditorGroupPart.AddSelected => new Rect2(Pad + 200, 58, 130, 28),
            EditorGroupPart.RemoveSelected => new Rect2(Pad + 338, 58, 150, 28),
            EditorGroupPart.SelectMembers => new Rect2(Pad + 496, 58, 140, 28),
            EditorGroupPart.AddMove => new Rect2(Pad, 100, 100, 28),
            EditorGroupPart.AddColor => new Rect2(Pad + 108, 100, 100, 28),
            EditorGroupPart.SnapToggle => new Rect2(Pad + 216, 100, 110, 28),
            EditorGroupPart.ScrollLeft => new Rect2(right - 4 * 40 - 3 * 4, 100, 40, 28),
            EditorGroupPart.ScrollRight => new Rect2(right - 3 * 40 - 2 * 4, 100, 40, 28),
            EditorGroupPart.ZoomOut => new Rect2(right - 2 * 40 - 4, 100, 40, 28),
            EditorGroupPart.ZoomIn => new Rect2(right - 40, 100, 40, 28),
            EditorGroupPart.Hint => new Rect2(Pad, dialog.Y - 52, dialog.X - 2 * Pad, 44),
            EditorGroupPart.RowIndex => new Rect2(Pad, rowTop, 24, 28),
            EditorGroupPart.RowKind => new Rect2(Pad + 30, rowTop, 64, 28),
            EditorGroupPart.RowStart => new Rect2(Pad + 100, rowTop, 64, 28),
            EditorGroupPart.RowEnd => new Rect2(Pad + 170, rowTop, 64, 28),
            EditorGroupPart.RowA => new Rect2(Pad + 240, rowTop, 64, 28),
            EditorGroupPart.RowB => new Rect2(Pad + 310, rowTop, 64, 28),
            EditorGroupPart.RowEase => new Rect2(Pad + 380, rowTop, 74, 28),
            EditorGroupPart.RowDelete => new Rect2(Pad + 460, rowTop, 30, 28),
            EditorGroupPart.RowTrack => RowTrack(dialog, row),
            _ => default,
        };
    }

    /// <summary>The seconds between ticks on the ruler: the beat grid's step, thinned so the ticks stay readable.</summary>
    public static double TickSeconds(Playhead playhead, float viewSpan, float rulerWidth)
    {
        double tick = TimingGrid.TickMs(playhead.Bpm, playhead.SnapDivision) / 1000.0;
        while (tick > 0 && viewSpan / tick * 1.0 > rulerWidth / 6.0)
            tick *= 2;
        return tick;
    }

    /// <summary>The nearest beat-grid time to <paramref name="seconds"/>, never before 0.</summary>
    public static float SnapSeconds(float seconds, Playhead playhead)
    {
        double tickMs = TimingGrid.TickMs(playhead.Bpm, playhead.SnapDivision);
        double offset = playhead.OffsetMs;
        int tick = (int)Math.Round((seconds * 1000.0 - offset) / tickMs);
        double snappedMs = TimingGrid.TickTime(tick, playhead.OffsetMs, tickMs);
        return Math.Max(0f, (float)(snappedMs / 1000.0));
    }

    /// <summary>Rounds a typed or dragged time to the milliseconds a level can sensibly hold.</summary>
    public static float Tidy(float seconds) => (float)Math.Round(Math.Max(0f, seconds), 3);
}

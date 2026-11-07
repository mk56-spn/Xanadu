using System;
using Godot;
using Xanadu.Ecs;
using Xanadu.Levels;

namespace Xanadu.Systems.Editor;

/// <summary>
/// Where each piece of the group overlay sits, in pixels from the dialog's
/// top-left corner, and the time-to-pixel maths of its timeline. Shared by the
/// builder, the system and the tests so they cannot disagree.
/// </summary>
public static class EditorGroupLayout
{
    /// <summary>Effects shown at once; a group can hold more, but only these rows are editable.</summary>
    public const int MaxRows = 8;

    /// <summary>The sidebar grid's cells and the gap between them.</summary>
    public const float CellWidth = 32f;
    public const float CellHeight = 28f;
    public const float CellGap = 3f;

    /// <summary>Room the groups grid takes in the sidebar: its label, four rows of cells and a margin.</summary>
    public const float GridHeight = 20f + 4 * (CellHeight + CellGap) + 6f;

    /// <summary>The group bar pinned at the top centre of the view, and the gap under it.</summary>
    public static readonly Vector2 BarSize = new(650f, 36f);
    private const float BarGap = 6f;

    public const float RowHeight = 30f;
    public const float Pad = 12f;
    public const int TickSlots = 96;
    public const int LabelSlots = 9;

    /// <summary>How opaque the panel's backing is: the field shows through it.</summary>
    public const float BackingAlpha = 0.1f;

    /// <summary>
    /// The three toolbar rows: the group itself, its effects and the timeline's
    /// controls, then its item-shader track.
    /// </summary>
    private const float GroupBarTop = 8f;
    private const float EffectBarTop = 44f;
    private const float CycleBarTop = 80f;
    private const float ShaderBarTop = 116f;
    private const float BarHeight = 28f;
    private const float RowsTop = 178f;

    /// <summary>The key lane sits under the last effect row.</summary>
    private const float LaneTop = RowsTop + MaxRows * RowHeight;

    /// <summary>How tall the timing lines and the playhead run: every effect row and the key lane.</summary>
    public const float LinesHeight = (MaxRows + 1) * RowHeight;
    private const float TableWidth = 500f;
    private const float Gap = 6f;
    private const float SectionGap = 16f;

    /// <summary>
    /// Where the panel sits: docked to the top of the editor viewport, between
    /// the tool strip and the right panel, so the field stays in view under it
    /// and the groups play live there.
    /// </summary>
    public static Rect2 PanelArea(Vector2 screen) => new(
        EditorShellMetrics.ToolStripWidth,
        BarArea(screen).End.Y + BarGap,
        ViewWidth(screen),
        LaneTop + RowHeight + 30f);

    /// <summary>
    /// The group bar: pinned at the top centre of the view, between the tool
    /// strip and the right panel. The panel docks under it.
    /// </summary>
    public static Rect2 BarArea(Vector2 screen)
    {
        float left = EditorShellMetrics.ToolStripWidth;
        float centre = left + ViewWidth(screen) / 2f;
        return new Rect2(
            centre - BarSize.X / 2f,
            EditorShellMetrics.HeaderHeight + BarGap,
            BarSize.X,
            BarSize.Y);
    }

    private static float ViewWidth(Vector2 screen) =>
        Math.Max(640f, screen.X - EditorShellMetrics.ToolStripWidth - EditorShellMetrics.RightPanelWidth);

    public static Vector2 DialogSize(Vector2 screen) => PanelArea(screen).Size;

    /// <summary>The panel's top-left corner on screen; every piece is placed from it.</summary>
    public static Vector2 Origin(Vector2 screen) => PanelArea(screen).Position;

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
        return new Rect2(timeline.Position.X, RowsTop - 26f, timeline.Size.X, 24f);
    }

    /// <summary>The explanatory line under the rows.</summary>
    public static Rect2 HintArea(Vector2 dialog) =>
        new(Pad, LaneTop + RowHeight + 6f, dialog.X - 2 * Pad, 20f);

    /// <summary>
    /// The key lane: the selected shader setting's keys as pins on the
    /// timeline's time scale, under the effect rows.
    /// </summary>
    public static Rect2 KeyLane(Vector2 dialog)
    {
        Rect2 timeline = Timeline(dialog);
        return new Rect2(timeline.Position.X, LaneTop, timeline.Size.X, RowHeight);
    }

    /// <summary>How close to a key's pin, in pixels, a press picks it.</summary>
    public const float KeyGrab = 9f;

    /// <summary>The small easing-curve display at the end of the selected effect's tools.</summary>
    public static Rect2 CurveArea(Vector2 dialog) =>
        new(After(EditorGroupPart.SelectionPick, dialog) + 8f, EffectBarTop, 44f, BarHeight);

    /// <summary>The colour palette popover, centred over the panel.</summary>
    public static Rect2 PalettePanel(Vector2 dialog) =>
        new((dialog.X - 330f) / 2f, (dialog.Y - 190f) / 2f, 330f, 190f);

    /// <summary>
    /// The toolbar pieces in reading order, as (part, width, gap before it).
    /// Left runs start at the left edge, right runs end at the right edge.
    /// </summary>
    private static readonly (EditorGroupPart Part, float Width, float Before)[] GroupBarLeft =
    {
        (EditorGroupPart.Title, 92f, 0f),
        (EditorGroupPart.NameField, 200f, Gap),
        (EditorGroupPart.MemberText, 150f, SectionGap),
        (EditorGroupPart.AddSelected, 110f, Gap),
        (EditorGroupPart.RemoveSelected, 110f, Gap),
        (EditorGroupPart.SelectMembers, 80f, Gap),
        (EditorGroupPart.InteriorLink, 110f, SectionGap),
        (EditorGroupPart.OutlineLink, 110f, Gap),
    };

    private static readonly (EditorGroupPart Part, float Width, float Before)[] GroupBarRight =
    {
        (EditorGroupPart.SoloToggle, 84f, 0f),
        (EditorGroupPart.Close, 84f, Gap),
    };

    private static readonly (EditorGroupPart Part, float Width, float Before)[] EffectBarLeft =
    {
        (EditorGroupPart.AddMove, 84f, 0f),
        (EditorGroupPart.AddColor, 84f, Gap),
        (EditorGroupPart.BufferToggle, 84f, Gap),
        (EditorGroupPart.SelectionText, 150f, SectionGap),
        (EditorGroupPart.SelectionTarget, 70f, Gap),
        (EditorGroupPart.SelectionDuplicate, 52f, Gap),
        (EditorGroupPart.SelectionMute, 70f, Gap),
        (EditorGroupPart.SelectionNudgeLeft, 30f, Gap),
        (EditorGroupPart.SelectionNudgeRight, 30f, 4f),
        (EditorGroupPart.SelectionPick, 54f, Gap),
    };

    private static readonly (EditorGroupPart Part, float Width, float Before)[] ShaderBarLeft =
    {
        (EditorGroupPart.TrackItem, 104f, 0f),
        (EditorGroupPart.TrackBuffer, 76f, 2f),
        (EditorGroupPart.ShaderText, 70f, Gap),
        (EditorGroupPart.ShaderSlot, 150f, Gap),
        (EditorGroupPart.ShaderParam, 110f, Gap),
        (EditorGroupPart.ShaderChannel, 56f, Gap),
        (EditorGroupPart.ShaderValue, 110f, 8f),
        (EditorGroupPart.ShaderDown, 30f, 4f),
        (EditorGroupPart.ShaderUp, 30f, 4f),
        (EditorGroupPart.ShaderClear, 56f, SectionGap),
        (EditorGroupPart.BufferTint, 110f, SectionGap),
        (EditorGroupPart.BufferAlphaText, 50f, Gap),
        (EditorGroupPart.BufferAlphaDown, 30f, 4f),
        (EditorGroupPart.BufferAlphaUp, 30f, 4f),
        (EditorGroupPart.BufferBlend, 110f, Gap),
    };

    /// <summary>The selected effect's repeat: count, spacing in beats, and how each cycle plays.</summary>
    private static readonly (EditorGroupPart Part, float Width, float Before)[] CycleBarLeft =
    {
        (EditorGroupPart.CycleTitle, 104f, 0f),
        (EditorGroupPart.CycleCount, 64f, Gap),
        (EditorGroupPart.CycleCountDown, 30f, 4f),
        (EditorGroupPart.CycleCountUp, 30f, 4f),
        (EditorGroupPart.CycleEveryText, 130f, SectionGap),
        (EditorGroupPart.CycleEveryDown, 30f, 4f),
        (EditorGroupPart.CycleEveryUp, 30f, 4f),
        (EditorGroupPart.CycleMode, 130f, SectionGap),
    };

    private static readonly (EditorGroupPart Part, float Width, float Before)[] EffectBarRight =
    {
        (EditorGroupPart.PlayToggle, 76f, 0f),
        (EditorGroupPart.SnapToggle, 96f, Gap),
        (EditorGroupPart.ScrollLeft, 32f, SectionGap),
        (EditorGroupPart.ScrollRight, 32f, 4f),
        (EditorGroupPart.ZoomOut, 32f, 12f),
        (EditorGroupPart.ZoomIn, 32f, 4f),
    };

    private static float After(EditorGroupPart part, Vector2 dialog)
    {
        Rect2 rect = Place(part, 0, dialog);
        return rect.End.X;
    }

    /// <summary>A piece's rectangle in one of the toolbar runs, or false when it isn't in a toolbar.</summary>
    private static bool TryToolbar(EditorGroupPart part, Vector2 dialog, out Rect2 rect)
    {
        return TryRun(GroupBarLeft, part, GroupBarTop, Pad, false, dialog, out rect) ||
               TryRun(GroupBarRight, part, GroupBarTop, dialog.X - Pad, true, dialog, out rect) ||
               TryRun(EffectBarLeft, part, EffectBarTop, Pad, false, dialog, out rect) ||
               TryRun(EffectBarRight, part, EffectBarTop, dialog.X - Pad, true, dialog, out rect) ||
               TryRun(ShaderBarLeft, part, ShaderBarTop, Pad, false, dialog, out rect) ||
               TryRun(CycleBarLeft, part, CycleBarTop, Pad, false, dialog, out rect);
    }

    private static bool TryRun(
        (EditorGroupPart Part, float Width, float Before)[] run,
        EditorGroupPart part,
        float top,
        float edge,
        bool fromRight,
        Vector2 dialog,
        out Rect2 rect)
    {
        float total = 0f;
        foreach (var piece in run)
            total += piece.Before + piece.Width;

        float x = fromRight ? edge - total : edge;
        foreach (var piece in run)
        {
            x += piece.Before;
            if (piece.Part == part)
            {
                rect = new Rect2(x, top, piece.Width, BarHeight);
                return true;
            }

            x += piece.Width;
        }

        rect = default;
        return false;
    }

    /// <summary>
    /// Where an effect sits on the timeline, on screen: from its start pin to
    /// its end pin across its row. An instant effect still gets a sliver to grab.
    /// <paramref name="timeline"/> is the timeline in screen space.
    /// </summary>
    public static Rect2 BarRect(GroupEffect effect, int row, float viewStart, float viewSpan, Rect2 timeline)
    {
        float left = XForTime(timeline, viewStart, viewSpan, effect.Start);
        float right = XForTime(timeline, viewStart, viewSpan, effect.End);
        float width = Math.Max(8f, right - left);
        float top = timeline.Position.Y + row * RowHeight + 4f;
        return new Rect2(left, top, width, RowHeight - 8f);
    }

    /// <summary>One effect row of the timeline, on screen, for drawing its band.</summary>
    public static Rect2 RowRect(int row, Rect2 timeline) =>
        new(timeline.Position.X, timeline.Position.Y + row * RowHeight, timeline.Size.X, RowHeight);

    public static float XForTime(Rect2 timeline, float viewStart, float viewSpan, float seconds) =>
        timeline.Position.X + (seconds - viewStart) / viewSpan * timeline.Size.X;

    public static float TimeForX(Rect2 timeline, float viewStart, float viewSpan, float x) =>
        viewStart + (x - timeline.Position.X) / timeline.Size.X * viewSpan;

    /// <summary>The rectangle of a piece, relative to the dialog's top-left.</summary>
    public static Rect2 Place(EditorGroupPart part, int row, Vector2 dialog)
    {
        if (TryToolbar(part, dialog, out Rect2 tool))
            return tool;

        float rowTop = RowsTop + row * RowHeight + 2f;
        Rect2 palette = PalettePanel(dialog);
        return part switch
        {
            EditorGroupPart.Scrim => new Rect2(Vector2.Zero, dialog),
            EditorGroupPart.Dialog => new Rect2(Vector2.Zero, dialog),
            EditorGroupPart.Hint => HintArea(dialog),
            EditorGroupPart.PlayheadLine => new Rect2(0, RowsTop, 2, LinesHeight),
            EditorGroupPart.PalettePanel => palette,
            EditorGroupPart.PaletteTitle => new Rect2(palette.Position.X + 16, palette.Position.Y + 8, 120, 24),
            EditorGroupPart.PaletteClose => new Rect2(palette.End.X - 38, palette.Position.Y + 8, 30, 24),
            EditorGroupPart.PaletteSwatch => new Rect2(
                palette.Position.X + 16 + (row % 6) * 50,
                palette.Position.Y + 42 + (row / 6) * 36,
                44,
                30),
            EditorGroupPart.PaletteAlpha => new Rect2(
                palette.Position.X + 16 + row * 76,
                palette.Position.Y + 146,
                68,
                26),
            EditorGroupPart.RowIndex => new Rect2(Pad, rowTop, 24, 26),
            EditorGroupPart.RowKind => new Rect2(Pad + 30, rowTop, 64, 26),
            EditorGroupPart.RowStart => new Rect2(Pad + 100, rowTop, 64, 26),
            EditorGroupPart.RowEnd => new Rect2(Pad + 170, rowTop, 64, 26),
            EditorGroupPart.RowA => new Rect2(Pad + 240, rowTop, 64, 26),
            EditorGroupPart.RowB => new Rect2(Pad + 310, rowTop, 64, 26),
            EditorGroupPart.RowEase => new Rect2(Pad + 380, rowTop, 74, 26),
            EditorGroupPart.RowDelete => new Rect2(Pad + 460, rowTop, 30, 26),
            EditorGroupPart.KeyName => new Rect2(Pad, LaneTop + 2f, 94, 26),
            EditorGroupPart.KeyTime => new Rect2(Pad + 100, LaneTop + 2f, 64, 26),
            EditorGroupPart.KeyValue => new Rect2(Pad + 170, LaneTop + 2f, 204, 26),
            EditorGroupPart.KeyEase => new Rect2(Pad + 380, LaneTop + 2f, 74, 26),
            EditorGroupPart.KeyDelete => new Rect2(Pad + 460, LaneTop + 2f, 30, 26),
            _ => default,
        };
    }

    /// <summary>The group grid's step in beats: <see cref="Playhead.SnapDivision"/> lines a beat.</summary>
    public static double StepBeats(Playhead playhead) => 1.0 / Math.Max(1, playhead.SnapDivision);

    /// <summary>
    /// The beats between ruler ticks over a window: the grid's step, doubled
    /// until the ticks stay readable (at most one per six pixels).
    /// </summary>
    public static double TickBeats(Playhead playhead, float viewStart, float viewSpan, float rulerWidth)
    {
        double beats = playhead.Tempo.BeatAt((viewStart + viewSpan) * 1000.0) - playhead.Tempo.BeatAt(viewStart * 1000.0);
        double step = StepBeats(playhead);
        while (beats / step > rulerWidth / 6.0)
            step *= 2;
        return step;
    }

    /// <summary>The nearest beat-grid time to <paramref name="seconds"/>, never before 0.</summary>
    public static float SnapSeconds(float seconds, Playhead playhead) =>
        Math.Max(0f, (float)(playhead.Tempo.Snap(seconds * 1000.0, StepBeats(playhead)) / 1000.0));

    /// <summary>A moment moved by <paramref name="lines"/> grid steps in beat space, so it keeps its place across tempo changes.</summary>
    public static float StepSeconds(float seconds, Playhead playhead, int lines) =>
        (float)(playhead.Tempo.TimeAt(playhead.Tempo.BeatAt(seconds * 1000.0) + StepBeats(playhead) * lines) / 1000.0);

    /// <summary>Rounds a typed or dragged time to the milliseconds a level can sensibly hold.</summary>
    public static float Tidy(float seconds) => (float)Math.Round(Math.Max(0f, seconds), 3);
}

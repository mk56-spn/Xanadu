using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Friflo.Engine.ECS;
using Xanadu.Ui.Editing;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Ui.Ecs.Ui;

/// <summary>
/// Where a text editor's text sits on screen: its area, the first line shown,
/// the rows drawn last frame (a line, or the note under a marked line) and the
/// blocks folded away in each text it has shown (<see cref="TextFolds"/>).
/// Turns points into places in the text and back.
/// </summary>
public sealed class TextEditorLayout(Rect2 area)
{
    public const int FontSize = 14;
    public const float LineHeight = 20;
    public const float Gutter = 44;
    public const float Inset = 10;

    /// <summary>The canvas's text sits this far right of its box's left edge.</summary>
    public const float TextPadding = 4;

    private readonly TextLineDecor _scratch = new();

    /// <summary>Each text's folds, kept with the text: an editor showing several in turn (tabs) keeps each one's.</summary>
    private readonly ConditionalWeakTable<TextBuffer, TextFolds> _folds = [];

    public Rect2 Area { get; set; } = area;

    /// <summary>One character's width in the monospaced face.</summary>
    public float CharWidth { get; } = UiTextMetrics.MonoCharWidth(FontSize);

    /// <summary>The first line shown.</summary>
    public int Scroll { get; set; }

    /// <summary>Where the caret was drawn last frame (blinking or not); null when its line is scrolled away.</summary>
    public Rect2? CaretRect { get; internal set; }

    /// <summary>The editor this lays out, which its owner's signals are emitted on.</summary>
    public Entity Editor { get; set; }

    /// <summary>What the last drawing was made from; the next frame redraws only when this moves.</summary>
    internal TextEditorDrawn? Drawn { get; set; }

    /// <summary>Rows that fit: a line takes one, a note one more.</summary>
    public int VisibleRows => (int)((Area.Size.Y - Inset * 2) / LineHeight);

    public float TextLeft => Area.Position.X + Gutter + Inset;

    /// <summary>The rows drawn last frame, top to bottom: their top, their line, and for a note row the marked column.</summary>
    internal List<(float Top, int Line, int? NoteColumn)> Rows { get; } = [];

    /// <summary>Where each folded line's pill was drawn last frame: a press on one opens it.</summary>
    internal List<(Rect2 Pill, int Line)> Pills { get; } = [];

    /// <summary><paramref name="buffer"/>'s folds, brought up to date with its text.</summary>
    public TextFolds Folds(TextBuffer buffer)
    {
        TextFolds folds = _folds.GetOrCreateValue(buffer);
        folds.Update(buffer, Editor.IsNull ? null : Editor.GetComponent<TextEditor>().LineComment);
        return folds;
    }

    /// <summary>
    /// Folds the block at <paramref name="header"/>, or opens it when folded. A caret it would hide goes to the end of
    /// the header, so folding never opens again at once.
    /// </summary>
    public void ToggleFold(TextBuffer buffer, int header)
    {
        TextFolds folds = Folds(buffer);
        if (folds.IsFolded(header))
        {
            folds.Open(header);
            return;
        }

        folds.Fold(header);
        if (folds.Hiding(buffer.Line) >= 0)
            buffer.Place(header, buffer.Lines[header].Length);
    }

    /// <summary>The fold a press at <paramref name="point"/> toggles: in the gutter of a line that folds, or on a folded line's pill; null elsewhere.</summary>
    public int? FoldUnder(TextBuffer buffer, Vector2 point)
    {
        if (!Area.HasPoint(point))
            return null;
        foreach ((Rect2 pill, int line) in Pills)
        {
            if (pill.HasPoint(point))
                return line;
        }

        if (point.X >= TextLeft)
            return null;
        TextFolds folds = Folds(buffer);
        foreach ((float top, int line, int? note) in Rows)
        {
            if (note is null && point.Y >= top && point.Y < top + LineHeight)
                return folds.CanFold(line) ? line : null;
        }

        return null;
    }

    /// <summary>
    /// The line and column under a point (a note row gives its mark's place), or null outside the text.
    /// While dragging a point outside still counts: above or below it gives the line just past the edge,
    /// so holding it there scrolls.
    /// </summary>
    public (int Line, int Column)? Hit(TextBuffer buffer, Vector2 point, bool dragging = false)
    {
        if (Rows.Count == 0 || (!dragging && !Area.HasPoint(point)))
            return null;

        if (dragging)
        {
            TextFolds folds = Folds(buffer);
            int edge = point.Y < Area.Position.Y ? Math.Max(0, folds.Previous(Rows[0].Line))
                : point.Y >= Area.End.Y ? folds.Step(Rows[^1].Line, 1, buffer.Lines.Count)
                : -1;
            if (edge >= 0)
                return (edge, Column(buffer, edge, point.X));
        }

        var row = Rows[^1];
        foreach (var candidate in Rows)
        {
            if (point.Y < candidate.Top + LineHeight)
            {
                row = candidate;
                break;
            }
        }

        if (row.NoteColumn is { } column && !dragging)
            return (row.Line, Math.Max(0, column));

        return (row.Line, Column(buffer, row.Line, point.X));
    }

    /// <summary>The character under a point and the bottom of its row; null off the text, past a line's end, on an inlay or a note.</summary>
    public (int Line, int Column, float Bottom)? Under(TextBuffer buffer, Vector2 point)
    {
        if (!Area.HasPoint(point) || point.X < TextLeft)
            return null;
        foreach ((float top, int index, int? note) in Rows)
        {
            if (point.Y < top || point.Y >= top + LineHeight)
                continue;
            if (note is not null || index >= buffer.Lines.Count)
                return null;
            string line = buffer.Lines[index];
            int visual = (int)((point.X - TextLeft) / CharWidth), shift = 0;
            foreach (TextInlay inlay in Decorate(buffer, index).Inlays)
            {
                int at = inlay.Column + shift;
                if (visual < at)
                    break;
                if (visual < at + inlay.Label.Length + 1)
                    return null;
                shift += inlay.Label.Length + 1;
            }

            int column = visual - shift;
            return column < line.Length ? (index, column, top + LineHeight) : null;
        }

        return null;
    }

    /// <summary>Opens any fold hiding a line, then scrolls just enough to keep it, and its note if it has one, in view.</summary>
    public void Reveal(TextBuffer buffer, int line)
    {
        TextFolds folds = Folds(buffer);
        folds.Show(line);
        if (line < Scroll)
        {
            Scroll = line;
            return;
        }

        while (Scroll < line && RowsFrom(buffer, folds, Scroll, line) > VisibleRows)
            Scroll = folds.Next(Scroll);
    }

    /// <summary>Where a text column is drawn: inlays at or before it push it right, so the caret sits after one.</summary>
    public static int VisualColumn(List<TextInlay> inlays, int column)
    {
        int visual = column;
        foreach (TextInlay inlay in inlays)
        {
            if (inlay.Column <= column)
                visual += inlay.Label.Length + 1;
        }

        return visual;
    }

    /// <summary>The owner's decoration of one line, in a scratch object that the next call reuses.</summary>
    internal TextLineDecor Decorate(TextBuffer buffer, int line)
    {
        _scratch.Clear();
        if (!Editor.IsNull)
            Editor.EmitSignal(new TextDecorate(buffer, line, _scratch));
        return _scratch;
    }

    private int Column(TextBuffer buffer, int line, float x)
    {
        int visual = Math.Max(0, (int)MathF.Round((x - TextLeft) / CharWidth));
        return LogicalColumn(Decorate(buffer, line).Inlays, visual, buffer.Lines[line].Length);
    }

    /// <summary>The rows the shown lines from <paramref name="first"/> to <paramref name="last"/> take, their notes included.</summary>
    private int RowsFrom(TextBuffer buffer, TextFolds folds, int first, int last)
    {
        int rows = 0;
        for (int line = first; line <= last && line < buffer.Lines.Count; line = folds.Next(line))
        {
            rows++;
            if (Decorate(buffer, line).Mark?.Note is not null)
                rows++;
        }

        return rows;
    }

    /// <summary>The text column drawn at a visual column; a click on an inlay lands just after it.</summary>
    private static int LogicalColumn(List<TextInlay> inlays, int visual, int length)
    {
        int shift = 0;
        foreach (TextInlay inlay in inlays)
        {
            int at = inlay.Column + shift;
            if (visual < at)
                break;
            if (visual <= at + inlay.Label.Length + 1)
                return inlay.Column;
            shift += inlay.Label.Length + 1;
        }

        return Math.Clamp(visual - shift, 0, length);
    }
}

/// <summary>Everything an editor's drawing depends on: when none of it moved, last frame's picture stands.</summary>
internal readonly record struct TextEditorDrawn(
    TextBuffer Buffer, int Version, int Line, int Column, (int Line, int Column)? Anchor, int Scroll, Rect2 Area, bool Focused, bool CaretOn,
    int OwnerVersion, Handle Item, int Folds);

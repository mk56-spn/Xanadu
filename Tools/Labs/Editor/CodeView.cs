using System;
using System.Collections.Generic;
using Xanadu.Ecs;
using Xanadu.Host.Text;
using Xanadu.Systems.Canvas;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Labs.Editor;

/// <summary>
/// Draws a <see cref="CodeBuffer"/> in the monospaced face: line numbers, the
/// caret's line, inlay hints, and Slang's problems (the line tinted, the word
/// underlined, the message after the code). Also turns a click into a line and
/// column, stepping over hints. Redrawn whole every frame; text shaping is
/// cached, and only the visible lines are drawn.
/// </summary>
internal sealed class CodeView
{
    public const int FontSize = 14;
    public const float LineHeight = 20;
    private const float Gutter = 44;
    private const float Inset = 10;

    /// <summary>The canvas's text sits this far right of its box's left edge.</summary>
    private const float TextPadding = 4;

    private static readonly Color Background = new("#0b1020");
    private static readonly Color GutterText = new("#4a5578");
    private static readonly Color Code = new("#d8def0");
    private static readonly Color Comment = new("#6b7a99");
    private static readonly Color Hint = new("#5d6b8f");
    private static readonly Color CaretLine = new(1, 1, 1, 0.04f);
    private static readonly Color ErrorLine = new(0.94f, 0.27f, 0.27f, 0.12f);
    private static readonly Color WarningLine = new(0.98f, 0.75f, 0.14f, 0.10f);

    private readonly IRectCanvas _canvas;
    private readonly Handle _item;
    private readonly float _charWidth;

    public CodeView(IRectCanvas canvas, Handle item, Rect2 area, HostText text)
    {
        _canvas = canvas;
        _item = item;
        Area = area;
        _charWidth = text.Width("MMMMMMMMMM", FontKind.Mono, FontSize) / 10f;
        CanvasServer.Current.SetCustomRect(item, true, area);
        CanvasServer.Current.SetClip(item, true);
    }

    public Rect2 Area { get; }

    /// <summary>The first line shown.</summary>
    public int Scroll { get; set; }

    public int VisibleLines => (int)((Area.Size.Y - Inset * 2) / LineHeight);

    private float TextLeft => Area.Position.X + Gutter + Inset;

    /// <summary>Scrolls just enough to keep a line in view.</summary>
    public void Reveal(int line)
    {
        if (line < Scroll)
            Scroll = line;
        else if (line >= Scroll + VisibleLines)
            Scroll = line - VisibleLines + 1;
    }

    public void Draw(CodeBuffer buffer, SlangHints hints, IReadOnlyList<SlangProblem> problems, bool caretOn)
    {
        _canvas.Clear(_item);
        _canvas.DrawRect(_item, Area, Background);

        int last = Math.Min(buffer.Lines.Count, Scroll + VisibleLines);
        for (int index = Scroll; index < last; index++)
        {
            float y = Area.Position.Y + Inset + (index - Scroll) * LineHeight;
            string line = buffer.Lines[index];
            List<InlayHint> lineHints = hints.For(line);

            SlangProblem? problem = FirstProblem(problems, index + 1);
            if (problem is { } shown)
                _canvas.DrawRect(_item, new Rect2(Area.Position.X, y, Area.Size.X, LineHeight), shown.Warning ? WarningLine : ErrorLine);
            else if (index == buffer.Line)
                _canvas.DrawRect(_item, new Rect2(Area.Position.X, y, Area.Size.X, LineHeight), CaretLine);

            _canvas.DrawTextMono(
                _item, new Rect2(Area.Position.X, y, Gutter - TextPadding, LineHeight), (index + 1).ToString(),
                index == buffer.Line ? Code : GutterText, FontSize, TextAlign.Right);
            int width = DrawLine(line, lineHints, y);

            if (problem is { } p)
                DrawProblem(line, lineHints, p, y, width);
            if (index == buffer.Line && caretOn)
            {
                float x = TextLeft + VisualColumn(lineHints, buffer.Column) * _charWidth;
                _canvas.DrawRect(_item, new Rect2(x - 1, y + 2, 2, LineHeight - 4), Palette.Accent);
            }
        }
    }

    /// <summary>The line and column under a point, or null outside the code.</summary>
    public (int Line, int Column)? Hit(CodeBuffer buffer, SlangHints hints, Vector2 point)
    {
        if (!Area.HasPoint(point))
            return null;

        int line = Math.Clamp(Scroll + (int)((point.Y - Area.Position.Y - Inset) / LineHeight), 0, buffer.Lines.Count - 1);
        int visual = Math.Max(0, (int)MathF.Round((point.X - TextLeft) / _charWidth));
        return (line, LogicalColumn(hints.For(buffer.Lines[line]), visual, buffer.Lines[line].Length));
    }

    /// <summary>Draws a line's code and hints; returns how many columns it took.</summary>
    private int DrawLine(string line, List<InlayHint> hints, float y)
    {
        int visual = 0, logical = 0;
        int comment = line.IndexOf("//", StringComparison.Ordinal);
        foreach (InlayHint hint in hints)
        {
            DrawCode(line, logical, hint.Column, comment, visual, y);
            visual += hint.Column - logical;
            logical = hint.Column;
            DrawAt(hint.Label, visual, y, Hint);
            visual += hint.Label.Length + 1;
        }

        DrawCode(line, logical, line.Length, comment, visual, y);
        return visual + line.Length - logical;
    }

    /// <summary>Code from one column to another, the part from <paramref name="comment"/> on dimmed.</summary>
    private void DrawCode(string line, int from, int to, int comment, int visual, float y)
    {
        if (to <= from)
            return;
        if (comment < 0 || comment >= to)
        {
            DrawAt(line[from..to], visual, y, Code);
            return;
        }

        int split = Math.Max(from, comment);
        if (split > from)
            DrawAt(line[from..split], visual, y, Code);
        DrawAt(line[split..to], visual + split - from, y, Comment);
    }

    private void DrawAt(string text, int visualColumn, float y, Color colour)
    {
        if (text.Trim().Length == 0)
            return;
        float x = TextLeft + visualColumn * _charWidth - TextPadding;
        _canvas.DrawTextMono(_item, new Rect2(x, y, text.Length * _charWidth + TextPadding * 2 + 8, LineHeight), text, colour, FontSize, TextAlign.Left);
    }

    /// <summary>The word at the problem underlined, and its message after the code.</summary>
    private void DrawProblem(string line, List<InlayHint> hints, SlangProblem problem, float y, int width)
    {
        Color colour = problem.Warning ? Palette.Warning : Palette.DangerBright;
        int start = Math.Clamp(problem.Column - 1, 0, line.Length);
        int end = start;
        while (end < line.Length && (char.IsLetterOrDigit(line[end]) || line[end] == '_'))
            end++;
        end = Math.Max(end, start + 1);
        float x0 = TextLeft + VisualColumn(hints, start) * _charWidth;
        float x1 = x0 + (end - start) * _charWidth;
        _canvas.DrawRect(_item, new Rect2(x0, y + LineHeight - 3, x1 - x0, 2), colour);
        DrawAt("← " + problem.Message, width + 3, y, colour);
    }

    private static SlangProblem? FirstProblem(IReadOnlyList<SlangProblem> problems, int line)
    {
        foreach (SlangProblem problem in problems)
        {
            if (problem.Line == line)
                return problem;
        }

        return null;
    }

    /// <summary>Where a text column is drawn: hints at or before it push it right, so the caret sits after a hint.</summary>
    private static int VisualColumn(List<InlayHint> hints, int column)
    {
        int visual = column;
        foreach (InlayHint hint in hints)
        {
            if (hint.Column <= column)
                visual += hint.Label.Length + 1;
        }

        return visual;
    }

    /// <summary>The text column drawn at a visual column; a click on a hint lands just after it.</summary>
    private static int LogicalColumn(List<InlayHint> hints, int visual, int length)
    {
        int shift = 0;
        foreach (InlayHint hint in hints)
        {
            int at = hint.Column + shift;
            if (visual < at)
                break;
            if (visual <= at + hint.Label.Length + 1)
                return hint.Column;
            shift += hint.Label.Length + 1;
        }

        return Math.Clamp(visual - shift, 0, length);
    }
}

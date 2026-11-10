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
/// caret's line, inlay hints as small pills, and Slang's problems (the line
/// tinted, the word underlined, the message on a row of its own under the
/// line, so it is never cut off by a long line). Also turns a click into a line
/// and column, stepping over hints. Redrawn whole every frame; text shaping is
/// cached, and only the visible rows are drawn.
/// </summary>
internal sealed class CodeView
{
    public const int FontSize = 14;
    public const float LineHeight = 20;
    private const int HintSize = 11;
    private const float Gutter = 44;
    private const float Inset = 10;

    /// <summary>The canvas's text sits this far right of its box's left edge.</summary>
    private const float TextPadding = 4;

    private static readonly Color Background = new("#0b1020");
    private static readonly Color GutterText = new("#4a5578");
    private static readonly Color Code = new("#d8def0");

    /// <summary>A fixed colour per kind of token (not editable yet).</summary>
    private static readonly Dictionary<TokenKind, Color> Colours = new()
    {
        [TokenKind.Plain] = Code,
        [TokenKind.Keyword] = new("#c792ea"),
        [TokenKind.Type] = new("#7fb8e6"),
        // Same blue, brighter as the width grows.
        [TokenKind.Width2] = new("#a3cdf2"),
        [TokenKind.Width3] = new("#c4e0fa"),
        [TokenKind.Width4] = new("#eaf5ff"),
        [TokenKind.LibraryType] = new("#ffcb6b"),
        [TokenKind.Namespace] = new("#89ddcb"),
        [TokenKind.Function] = new("#82aaff"),
        [TokenKind.Number] = new("#f78c6c"),
        [TokenKind.Text] = new("#c3e88d"),
        [TokenKind.Attribute] = new("#a0a8c4"),
        [TokenKind.Comment] = new("#6b7a99"),
    };
    private static readonly Color HintText = new("#93a4cf");
    private static readonly Color HintPill = new(0.55f, 0.65f, 0.95f, 0.10f);
    private static readonly Color CaretLine = new(1, 1, 1, 0.04f);
    private static readonly Color ErrorLine = new(0.94f, 0.27f, 0.27f, 0.12f);
    private static readonly Color WarningLine = new(0.98f, 0.75f, 0.14f, 0.10f);

    private readonly IRectCanvas _canvas;
    private readonly Handle _item;
    private readonly float _charWidth;

    /// <summary>What each drawn row shows, top to bottom: its top, its line, and the problem column for a message row.</summary>
    private readonly List<(float Top, int Line, int? ProblemColumn)> _rows = new();

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

    /// <summary>Rows that fit: a line takes one, a problem's message one more.</summary>
    public int VisibleRows => (int)((Area.Size.Y - Inset * 2) / LineHeight);

    private float TextLeft => Area.Position.X + Gutter + Inset;

    /// <summary>Scrolls just enough to keep a line, and its message if it has one, in view.</summary>
    public void Reveal(int line, IReadOnlyList<SlangProblem> problems)
    {
        if (line < Scroll)
        {
            Scroll = line;
            return;
        }

        while (Scroll < line && RowsFrom(Scroll, line, problems) > VisibleRows)
            Scroll++;
    }

    public void Draw(CodeBuffer buffer, SlangHints hints, IReadOnlyList<SlangProblem> problems, bool caretOn)
    {
        _canvas.Clear(_item);
        _canvas.DrawRect(_item, Area, Background);
        _rows.Clear();

        float bottom = Area.End.Y - Inset;
        float y = Area.Position.Y + Inset;
        for (int index = Scroll; index < buffer.Lines.Count && y + LineHeight <= bottom; index++)
        {
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
            DrawLine(line, lineHints, SlangSyntax.Tokens(line, hints.Types, hints.Namespaces), y);
            if (index == buffer.Line && caretOn)
            {
                float x = TextLeft + VisualColumn(lineHints, buffer.Column) * _charWidth;
                _canvas.DrawRect(_item, new Rect2(x - 1, y + 2, 2, LineHeight - 4), Palette.Accent);
            }

            _rows.Add((y, index, null));
            y += LineHeight;

            if (problem is { } p)
            {
                Underline(line, lineHints, p, y - LineHeight);
                if (y + LineHeight <= bottom)
                {
                    DrawMessage(line, p, y);
                    _rows.Add((y, index, p.Column));
                    y += LineHeight;
                }
            }
        }
    }

    /// <summary>The line and column under a point (a message row gives its problem's place), or null outside the code.</summary>
    public (int Line, int Column)? Hit(CodeBuffer buffer, SlangHints hints, Vector2 point)
    {
        if (!Area.HasPoint(point) || _rows.Count == 0)
            return null;

        (float Top, int Line, int? ProblemColumn) row = _rows[^1];
        foreach (var candidate in _rows)
        {
            if (point.Y < candidate.Top + LineHeight)
            {
                row = candidate;
                break;
            }
        }

        if (row.ProblemColumn is { } column)
            return (row.Line, Math.Max(0, column - 1));

        int visual = Math.Max(0, (int)MathF.Round((point.X - TextLeft) / _charWidth));
        return (row.Line, LogicalColumn(hints.For(buffer.Lines[row.Line]), visual, buffer.Lines[row.Line].Length));
    }

    private static int RowsFrom(int first, int last, IReadOnlyList<SlangProblem> problems)
    {
        int rows = last - first + 1;
        foreach (SlangProblem problem in problems)
        {
            if (problem.Line - 1 >= first && problem.Line - 1 <= last)
                rows++;
        }

        return rows;
    }

    private void DrawLine(string line, List<InlayHint> hints, List<Token> tokens, float y)
    {
        int visual = 0, logical = 0;
        foreach (InlayHint hint in hints)
        {
            DrawCode(line, tokens, logical, hint.Column, visual, y);
            visual += hint.Column - logical;
            logical = hint.Column;
            DrawHint(hint.Label, visual, y);
            visual += hint.Label.Length + 1;
        }

        DrawCode(line, tokens, logical, line.Length, visual, y);
    }

    /// <summary>A hint in a smaller face on a faint pill, so it can't be read as code or a comment.</summary>
    private void DrawHint(string label, int visualColumn, float y)
    {
        // The hint has label + 1 columns of room; its smaller text sits centred in a pill a little narrower.
        float room = (label.Length + 1) * _charWidth;
        float textWidth = label.Length * _charWidth * HintSize / FontSize;
        float pill = textWidth + 8;
        float x = TextLeft + visualColumn * _charWidth + (room - pill) / 2 - _charWidth / 2;
        _canvas.DrawRect(_item, new Rect2(x, y + 3, pill, LineHeight - 6), HintPill);
        _canvas.DrawTextMono(_item, new Rect2(x + 4 - TextPadding, y, textWidth + TextPadding * 2 + 8, LineHeight), label, HintText, HintSize, TextAlign.Left);
    }

    /// <summary>The code from one column to another, each token in its colour; punctuation between tokens is plain.</summary>
    private void DrawCode(string line, List<Token> tokens, int from, int to, int visual, float y)
    {
        int at = from;
        foreach (Token token in tokens)
        {
            int start = Math.Max(token.Start, from), end = Math.Min(token.End, to);
            if (end <= start)
                continue;
            if (start > at)
                DrawAt(line[at..start], visual + at - from, y, Code);
            DrawAt(line[start..end], visual + start - from, y, Colours[token.Kind]);
            at = end;
        }

        if (to > at)
            DrawAt(line[at..to], visual + at - from, y, Code);
    }

    private void DrawAt(string text, int visualColumn, float y, Color colour)
    {
        if (text.Trim().Length == 0)
            return;
        float x = TextLeft + visualColumn * _charWidth - TextPadding;
        _canvas.DrawTextMono(_item, new Rect2(x, y, text.Length * _charWidth + TextPadding * 2 + 8, LineHeight), text, colour, FontSize, TextAlign.Left);
    }

    /// <summary>The word at the problem underlined.</summary>
    private void Underline(string line, List<InlayHint> hints, SlangProblem problem, float y)
    {
        int start = Math.Clamp(problem.Column - 1, 0, line.Length);
        int end = start;
        while (end < line.Length && (char.IsLetterOrDigit(line[end]) || line[end] == '_'))
            end++;
        end = Math.Max(end, start + 1);
        float x0 = TextLeft + VisualColumn(hints, start) * _charWidth;
        _canvas.DrawRect(_item, new Rect2(x0, y + LineHeight - 3, (end - start) * _charWidth, 2), Colour(problem));
    }

    /// <summary>The message on its own row, lined up under the line's code, with a mark pointing up at it.</summary>
    private void DrawMessage(string line, SlangProblem problem, float y)
    {
        int indent = line.Length - line.TrimStart(' ').Length;
        _canvas.DrawRect(_item, new Rect2(Area.Position.X, y, Area.Size.X, LineHeight), problem.Warning ? WarningLine : ErrorLine);
        DrawAt("└ " + problem.Message, indent, y, Colour(problem));
    }

    private static Color Colour(SlangProblem problem) => problem.Warning ? Palette.Warning : Palette.DangerBright;

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

using System;
using System.Collections.Generic;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Labs.Editor;

/// <summary>What the editor's surroundings show this frame.</summary>
internal readonly record struct EditorState(
    int Line, int Column, string Status, Color StatusColour, bool PreviewStale, string PreviewNote,
    IReadOnlyList<SlangProblem> Problems);

/// <summary>
/// Everything round the code: the status bar under it (caret place, compile
/// result), a badge on the preview while it shows an older version, the
/// problem list under the preview (a click jumps to the line), and a short key
/// guide at the bottom of that column.
/// </summary>
internal sealed class EditorChrome
{
    private const float Pad = 12;
    private const float Row = 22;

    /// <summary>Problems shown at most; the rest of the column is for tuning values.</summary>
    private const int ProblemRows = 6;

    private static readonly Color Bar = new("#111830");
    private static readonly Color Column = new("#0e1428");
    private static readonly Color Badge = new(0.04f, 0.06f, 0.13f, 0.85f);

    private static readonly string[] Help =
    [
        "shade(ItemInput i) returns the pixel; import xanadu for shapes, color., ease. …",
        "Use time, mousePos, mouse or resolution and the lab fills it; no need to declare them.",
        "Shift selects · drag · double-click a word · Ctrl+A/C/X/V · Tab · wheel · click a problem to jump.",
    ];

    private readonly IRectCanvas _canvas;
    private readonly Handle _item;
    private readonly Rect2 _bar;
    private readonly Rect2 _preview;
    private readonly Rect2 _side;
    private readonly List<(Rect2 Box, SlangProblem Problem)> _problemRows = new();

    public EditorChrome(IRectCanvas canvas, Handle item, Rect2 bar, Rect2 preview, Rect2 side)
    {
        _canvas = canvas;
        _item = item;
        _bar = bar;
        _preview = preview;
        _side = side;
        float tuningTop = side.Position.Y + Pad + Row + 4 + ProblemRows * Row + Pad;
        float helpTop = side.End.Y - Pad - Help.Length * Row;
        TuningArea = new Rect2(side.Position.X + Pad, tuningTop, side.Size.X - Pad * 2, helpTop - Pad - tuningTop);
    }

    /// <summary>Where the value sliders go: between the problems and the key guide.</summary>
    public Rect2 TuningArea { get; }

    public void Draw(in EditorState state)
    {
        _canvas.Clear(_item);

        _canvas.DrawRect(_item, _bar, Bar);
        var barText = new Rect2(_bar.Position.X + Pad, _bar.Position.Y, _bar.Size.X - Pad * 2, _bar.Size.Y);
        _canvas.DrawTextStyledLeft(_item, barText, state.Status, state.StatusColour, 14);
        _canvas.DrawTextStyledRight(_item, barText, $"line {state.Line + 1}, col {state.Column + 1}", Palette.TextMuted, 14);

        if (state.PreviewStale)
        {
            var badge = new Rect2(_preview.Position.X + Pad, _preview.Position.Y + Pad, _preview.Size.X - Pad * 2, 30);
            _canvas.DrawRect(_item, badge, Badge);
            _canvas.DrawTextStyled(_item, badge, state.PreviewNote, Palette.Warning, 14);
        }

        _canvas.DrawRect(_item, _side, Column);
        float y = _side.Position.Y + Pad;
        _canvas.DrawTextStyledLeft(_item, new Rect2(_side.Position.X + Pad, y, _side.Size.X, Row), "PROBLEMS", Palette.TextDim, 13);
        y += Row + 4;

        _problemRows.Clear();
        float helpTop = _side.End.Y - Pad - Help.Length * Row;
        float problemsEnd = y + ProblemRows * Row;
        if (state.Problems.Count == 0)
            _canvas.DrawTextStyledLeft(_item, new Rect2(_side.Position.X + Pad, y, _side.Size.X, Row), "none", Palette.TextMuted, 14);
        foreach (SlangProblem problem in state.Problems)
        {
            if (y + Row > problemsEnd)
                break;
            var box = new Rect2(_side.Position.X + 4, y, _side.Size.X - 8, Row);
            string where = problem.Line > 0 ? $"{problem.Line,4}" : "    ";
            _canvas.DrawTextMono(_item, new Rect2(box.Position.X + Pad - 4, y, 48, Row), where, Palette.TextMuted, 13, TextAlign.Left);
            _canvas.DrawTextMono(
                _item, new Rect2(box.Position.X + Pad + 44, y, box.Size.X - 56, Row), Clip(problem.Message, 82),
                problem.Warning ? Palette.Warning : Palette.DangerBright, 13, TextAlign.Left);
            _problemRows.Add((box, problem));
            y += Row;
        }

        for (int i = 0; i < Help.Length; i++)
        {
            _canvas.DrawTextStyledLeft(
                _item, new Rect2(_side.Position.X + Pad, helpTop + i * Row, _side.Size.X - Pad * 2, Row), Help[i], Palette.TextMuted, 13);
        }
    }

    /// <summary>The problem whose row is under a point, if it has a place in the code.</summary>
    public SlangProblem? ProblemAt(Vector2 point)
    {
        foreach ((Rect2 box, SlangProblem problem) in _problemRows)
        {
            if (box.HasPoint(point) && problem.Line > 0)
                return problem;
        }

        return null;
    }

    private static string Clip(string text, int length) => text.Length > length ? text[..(length - 1)] + "…" : text;
}

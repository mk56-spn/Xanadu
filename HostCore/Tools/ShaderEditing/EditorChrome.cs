using System.Collections.Generic;
using Xanadu.Core.Theme;
using Xanadu.Slang;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Host.Tools.ShaderEditing;

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

    /// <summary>Rows of problems shown at most, a long one wrapping over several; the rest of the column is for tuning values.</summary>
    private const int ProblemRows = 6;

    private const int ProblemSize = 13;
    private static readonly float ProblemCharWidth = UiTextMetrics.MonoCharWidth(ProblemSize);

    private static readonly Color Bar = Palette.Surface;
    /// <summary>The column under the preview: filled by its node, the problems and guide drawn over it.</summary>
    public static readonly Color ColumnColour = Palette.SurfaceDeep;
    private static readonly Color Badge = new(0.04f, 0.06f, 0.13f, 0.85f);

    /// <summary>The lines under the values: the editor's own keys; the stage puts its preview's lines before them.</summary>
    public string[] Help
    {
        get => _help;
        set => _help = value;
    }

    /// <summary>How tall the problems' box is: its heading and the rows it shows.</summary>
    public const float ProblemsHeight = Pad + Row + 4 + ProblemRows * Row + Pad;

    /// <summary>How tall the key guide's box is with <paramref name="lines"/> of it shown.</summary>
    public static float HelpHeight(int lines) => lines * Row + Pad;

    /// <summary>The key guide's lines that fit in a box that tall.</summary>
    public int HelpFitting(float height) => Math.Clamp((int)((height - Pad) / Row), 0, Help.Length);

    private string[] _help =
    [
        "Shift selects · drag · double-click a word · Ctrl+A/C/X/V · Ctrl+Z/Y · wheel · click a problem to jump.",
        "Tab / Shift+Tab indent lines · Ctrl+/ comments · Ctrl+arrows and Ctrl+Backspace go by word · F2 renames.",
        "F12 or Ctrl+click goes to a declaration; the library opens in a tab, read-only until you say yes.",
        "Drag the bar left of the preview to resize it.",
    ];

    private readonly IRectCanvas _canvas;
    private readonly Handle _item;
    private Rect2 _bar;
    private Rect2 _preview;
    private Rect2 _problems;
    private Rect2 _guide;
    private readonly List<(Rect2 Box, SlangProblem Problem)> _problemRows = [];

    public EditorChrome(IRectCanvas canvas, Handle item)
    {
        _canvas = canvas;
        _item = item;
    }

    /// <summary>
    /// Where it draws, from the laid-out boxes: the bar under the code, the preview, and in the
    /// column under it the problems' and the key guide's boxes (the column fills its own background).
    /// </summary>
    public void Place(Rect2 bar, Rect2 preview, Rect2 problems, Rect2 help)
    {
        _bar = bar;
        _preview = preview;
        _problems = problems;
        _guide = help;
    }

    /// <summary>Rows the values always get before the key guide gives way: the header and a few values.</summary>
    public const int MinTuningRows = 5;

    /// <summary>The height of a row of the guide or the problems.</summary>
    public const float RowHeight = Row;

    /// <summary>The bar between the code and the column that drags to resize them; lit while hovered or held.</summary>
    public Rect2 Divider { get; set; }

    public bool DividerLit { get; set; }

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

        float grip = DividerLit ? 4 : 2;
        _canvas.DrawRect(_item, new Rect2(Divider.GetCenter().X - grip / 2, Divider.Position.Y, grip, Divider.Size.Y),
            DividerLit ? Palette.PrimaryMuted.Lerp(Colors.White, 0.16f) : Palette.PrimaryMuted);

        Rect2 side = _problems;
        float y = side.Position.Y + Pad;
        _canvas.DrawTextStyledLeft(_item, new Rect2(side.Position.X + Pad, y, side.Size.X, Row), "PROBLEMS", Palette.TextDim, 13);
        y += Row + 4;

        _problemRows.Clear();
        float problemsEnd = y + ProblemRows * Row;
        if (state.Problems.Count == 0)
            _canvas.DrawTextStyledLeft(_item, new Rect2(side.Position.X + Pad, y, side.Size.X, Row), "none", Palette.TextMuted, 14);
        float messageX = side.Position.X + Pad + 48, messageWidth = side.Size.X - 4 - Pad - 48;
        int fits = Math.Max(8, (int)(messageWidth / ProblemCharWidth));
        foreach (SlangProblem problem in state.Problems)
        {
            if (y + Row > problemsEnd)
                break;
            List<string> rows = UiTextFit.WrapColumns(problem.Message, fits);
            int shown = Math.Min(rows.Count, (int)((problemsEnd - y) / Row));
            if (shown < rows.Count)
                rows[shown - 1] = Clip(rows[shown - 1], fits);
            var box = new Rect2(side.Position.X + 4, y, side.Size.X - 8, shown * Row);
            string where = problem.Line > 0 ? $"{problem.Line,4}" : "    ";
            _canvas.DrawTextMono(_item, new Rect2(box.Position.X + Pad - 4, y, 48, Row), where, Palette.TextMuted, ProblemSize, TextAlign.Left);
            for (int r = 0; r < shown; r++)
            {
                _canvas.DrawTextMono(
                    _item, new Rect2(messageX, y + r * Row, messageWidth, Row), rows[r],
                    problem.Warning ? Palette.Warning : Palette.DangerBright, ProblemSize, TextAlign.Left);
            }

            _problemRows.Add((box, problem));
            y += shown * Row;
        }

        int lines = HelpFitting(_guide.Size.Y);
        for (int i = 0; i < lines; i++)
        {
            _canvas.DrawTextStyledLeft(
                _item, new Rect2(_guide.Position.X + Pad, _guide.Position.Y + i * Row, _guide.Size.X - Pad * 2, Row), Help[i], Palette.TextMuted, 13);
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

    /// <summary>The last row that fits, marked as going on.</summary>
    private static string Clip(string text, int length) => text.Length >= length ? text[..(length - 1)] + "…" : text + "…";
}

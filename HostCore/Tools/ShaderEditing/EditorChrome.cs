using System.Collections.Generic;
using System.Globalization;
using Friflo.Engine.ECS;
using Xanadu.Core.Theme;
using Xanadu.Slang;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Host.Tools.ShaderEditing;

/// <summary>What the editor's surroundings show this frame.</summary>
internal readonly record struct EditorState(
    int Line, int Column, string Status, Color StatusColour, bool PreviewStale, string PreviewNote,
    IReadOnlyList<SlangProblem> Problems);

/// <summary>
/// Everything round the code, as UI nodes in the editor's slots: the status bar under it (compile
/// result, caret place), a badge on the preview while it shows an older version (on a layer of its
/// own over the preview), the line in the divider, the problem list under the preview (a click
/// jumps to the line) and a short key guide at the bottom of that column. Built once; each frame
/// only what changed is set, so a still frame costs nothing.
/// </summary>
internal sealed class EditorChrome
{
    private const float Pad = UiMetrics.Space.Md;
    /// <summary>Round the status bar's text and the key guide: smaller, as the bar is one line tall.</summary>
    private const float Inset = UiMetrics.Space.Sm;
    private const float Row = UiMetrics.Control.Xs;
    private const float LineWidth = 48;

    /// <summary>Rows of problems shown at most, a long one wrapping over several; the rest of the column is for tuning values.</summary>
    private const int ProblemRows = 6;

    private static readonly float ProblemCharWidth = UiTextMetrics.MonoCharWidth(UiType.Caption);

    private static readonly Color Bar = Palette.Surface;
    /// <summary>The column under the preview: filled by its node, the problems and guide on it.</summary>
    public static readonly Color ColumnColour = Palette.SurfaceDeep;
    private static readonly Color Badge = new(0.04f, 0.06f, 0.13f, 0.85f);

    /// <summary>How tall the problems' box is: its heading and the rows it shows.</summary>
    public const float ProblemsHeight = Pad + Row + UiMetrics.Space.Xs + ProblemRows * Row + Pad;

    /// <summary>Rows the values always get before the key guide gives way: the header and a few values.</summary>
    public const int MinTuningRows = 5;

    /// <summary>The height of a row of the guide or the problems.</summary>
    public const float RowHeight = Row;

    private readonly UiNode _status, _place, _badgeLayer, _badgeFrame, _badge, _badgeNote, _dividerLine, _problemList, _none, _guide;
    private readonly List<(UiNode Row, SlangProblem Problem)> _problemRows = [];
    private IReadOnlyList<SlangProblem>? _listed;
    private float _listedWidth;
    private (int Line, int Column) _caret = (-1, -1);
    private Rect2 _framed;
    private bool _lit = true;
    private string[] _help = [];

    public EditorChrome(EntityStore entities, UiNode bar, UiNode divider, UiNode problems, UiNode guide)
    {
        UiNode barRow = bar.Fill(Bar).Cross(StackCrossAlignment.Stretch).Row().Grow().Pad(Inset).Gap(Pad).Cross(StackCrossAlignment.Center);
        _status = barRow.Text("", UiType.Small).Fit().Grow();
        // Sized once for the longest place it shows: its text changes, its box doesn't.
        _place = barRow.Text("", UiType.Small, Palette.TextMuted, TextAlign.Right)
            .Width(UiTextMetrics.Width("line 99999, col 9999", UiType.Small));

        _badgeLayer = UiNode.Layer(entities, default, UiLayers.ShaderPreviewNote, null, UiStage.Full).Theme(UiThemes.Tools);
        _badgeFrame = _badgeLayer.Column().IgnoreLayout();
        _badge = _badgeFrame.Row().Fill(Badge).Height(UiMetrics.Control.Sm).Main(StackMainAlignment.Center)
            .Cross(StackCrossAlignment.Center).Anchor(UiEdge.Top, Pad).Hidden();
        _badgeNote = _badge.Text("", UiType.Small, Palette.Warning, TextAlign.Center).Fit();

        _dividerLine = divider.Cross(StackCrossAlignment.Center).Rect(Palette.PrimaryMuted, size: new Vector2(UiMetrics.Border.Medium, 0)).Grow();
        DividerLit = false;

        problems.Pad(Pad).Gap(UiMetrics.Space.Xs).Cross(StackCrossAlignment.Stretch);
        problems.Text("PROBLEMS", UiType.Caption, Palette.TextDim).Height(Row);
        _problemList = problems.Column().Cross(StackCrossAlignment.Stretch);
        _none = problems.Text("none", UiType.Small, Palette.TextMuted).Height(Row);
        _guide = guide.Pad(Inset).Cross(StackCrossAlignment.Stretch);
    }

    /// <summary>The lines under the values: the editor's own keys; the stage puts its preview's lines before them.</summary>
    public string[] Help
    {
        get => _help;
        set
        {
            _help = value;
            UiLayout.DeleteChildren(_guide.Entity);
            foreach (string line in value)
                _guide.Text(line, UiType.Caption, Palette.TextMuted).Fit().Height(Row);
        }
    }

    /// <summary>How tall the key guide's box is with <paramref name="lines"/> of it shown.</summary>
    public static float HelpHeight(int lines) => lines * Row + Inset * 2;

    /// <summary>The key guide's lines that fit in a box that tall.</summary>
    public int HelpFitting(float height) => Math.Clamp((int)((height - Inset * 2) / Row), 0, Help.Length);

    /// <summary>The guide shows its first <paramref name="lines"/> lines, the rest hidden.</summary>
    public void ShowHelp(int lines)
    {
        int i = 0;
        foreach (Entity line in _guide.Entity.ChildEntities)
            UiLayout.SetHidden(line, i++ >= lines);
    }

    /// <summary>Lit while the divider is dragged or hovered: wider and brighter.</summary>
    public bool DividerLit
    {
        set
        {
            if (value == _lit)
                return;
            _lit = value;
            _dividerLine.Width(value ? UiMetrics.Border.Medium * 2 : UiMetrics.Border.Medium).Fill(value ? Palette.PrimaryMuted.Lerp(Colors.White, 0.16f) : Palette.PrimaryMuted);
        }
    }

    /// <summary>The badge follows the preview's laid-out box.</summary>
    public void Place(Rect2 preview)
    {
        if (preview == _framed || preview.Size.X <= 0)
            return;
        _framed = preview;
        _badgeFrame.Box(preview);
        _badge.Width(preview.Size.X - Pad * 2);
    }

    /// <summary>Sets what changed since the last frame.</summary>
    public void Show(in EditorState state)
    {
        _status.SetText(state.Status);
        UiText.SetColor(_status.Entity, state.StatusColour);
        if (_caret != (state.Line, state.Column))
        {
            _caret = (state.Line, state.Column);
            _place.SetText(string.Create(CultureInfo.InvariantCulture, $"line {state.Line + 1}, col {state.Column + 1}"));
        }

        UiLayout.SetHidden(_badge.Entity, !state.PreviewStale);
        _badgeNote.SetText(state.PreviewNote);

        float width = UiLayout.Box(_problemList.Entity).Size.X;
        if (!ReferenceEquals(state.Problems, _listed) || width != _listedWidth)
            List(state.Problems, width);
    }

    /// <summary>The problems again, a row each, wrapped to the list's width; as many rows as fit.</summary>
    private void List(IReadOnlyList<SlangProblem> problems, float width)
    {
        (_listed, _listedWidth) = (problems, width);
        UiLayout.DeleteChildren(_problemList.Entity);
        _problemRows.Clear();
        UiLayout.SetHidden(_none.Entity, problems.Count > 0);
        int fits = Math.Max(8, (int)((width - LineWidth - Pad) / ProblemCharWidth));
        int left = ProblemRows;
        foreach (SlangProblem problem in problems)
        {
            if (left == 0)
                break;
            List<string> rows = UiTextFit.WrapColumns(problem.Message, fits);
            int shown = Math.Min(rows.Count, left);
            if (shown < rows.Count)
                rows[shown - 1] = Clip(rows[shown - 1], fits);
            left -= shown;

            UiNode row = _problemList.Row().Cross(StackCrossAlignment.Start);
            row.Text(problem.Line > 0 ? $"{problem.Line,4}" : "", UiType.Caption, Palette.TextMuted).Mono().Size(LineWidth, Row);
            UiNode lines = row.Column().Grow().Cross(StackCrossAlignment.Stretch);
            Color ink = problem.Warning ? Palette.Warning : Palette.DangerBright;
            for (int r = 0; r < shown; r++)
                lines.Text(rows[r], UiType.Caption, ink).Mono().Fit().Height(Row);
            _problemRows.Add((row, problem));
        }
    }

    /// <summary>The problem whose row is under a point, if it has a place in the code.</summary>
    public SlangProblem? ProblemAt(Vector2 point)
    {
        foreach ((UiNode row, SlangProblem problem) in _problemRows)
        {
            if (UiLayout.Box(row.Entity).HasPoint(point) && problem.Line > 0)
                return problem;
        }

        return null;
    }

    public void Delete() => UiLayout.DeleteSubtree(_badgeLayer.Entity);

    /// <summary>The last row that fits, marked as going on.</summary>
    private static string Clip(string text, int length) => text.Length >= length ? text[..(length - 1)] + "…" : text + "…";
}

using System.Collections.Generic;
using System.Linq;
using Xanadu.Slang;
using Xanadu.Ui.Editing;

namespace Xanadu.Host.Tools.ShaderEditing;

/// <summary>
/// The shader's texts: one, or a stack's bricks. They are read as a whole (the clips,
/// the warnings, the compile) joined in pipeline order, and every place in the whole
/// maps back to its brick (<see cref="Unjoin"/>).
/// </summary>
internal sealed partial class ShaderCodeEditor
{
    /// <summary>The stack's name, as its bricks' labels carry it; null while editing one text.</summary>
    private readonly string? _stack;

    /// <summary>The shader's texts in pipeline order: the one text, or each brick.</summary>
    private readonly List<Tab> _bricks = [];

    /// <summary>A stack's pipeline over the code; none for one text.</summary>
    private readonly BrickStrip? _brickStrip;

    /// <summary>Goes up as bricks are added and removed.</summary>
    private int _structure;

    /// <summary>Where each text was in the whole the clips were read from.</summary>
    private List<Placed> _clipPlaces = [];

    /// <summary>A text in the joined whole: its line 1 is the whole's line <c>Start + 1</c>, and it has <c>Count</c> lines.</summary>
    private readonly record struct Placed(Tab Tab, int Start, int Count);

    /// <summary>The brick a stack opens on: the fragment's, else the first.</summary>
    private static Brick FirstOpen(IReadOnlyList<Brick> bricks) =>
        bricks.FirstOrDefault(brick => brick.Kind == BrickKind.Shade) ?? bricks[0];

    /// <summary>A text for each brick; the open one uses the editor's own buffer.</summary>
    private void MakeBricks(IReadOnlyList<Brick> bricks, Brick open, TextBuffer buffer, TextHistory history)
    {
        foreach (Brick brick in bricks.OrderBy(brick => brick.Kind))
        {
            Tab tab = ReferenceEquals(brick, open) ? NewBrick(brick, buffer, history) : NewBrick(brick);
            _bricks.Add(tab);
            if (ReferenceEquals(brick, open))
                _tabs.Add(tab);
        }
    }

    private Tab NewBrick(Brick brick)
    {
        var buffer = new TextBuffer();
        buffer.Load(brick.Text);
        return NewBrick(brick, buffer, new TextHistory(buffer));
    }

    private Tab NewBrick(Brick brick, TextBuffer buffer, TextHistory history) =>
        _stack is null
            ? new Tab(_view.Label, null, buffer, history, "")
            : new Tab(ShaderStack.FileName(brick.Kind), null, buffer, history, "") { Kind = brick.Kind };

    /// <summary>The label Slang's errors carry for a text's own lines.</summary>
    private string LabelOf(Tab tab) => _stack is null ? _view.Label : ShaderStack.Label(_stack, tab.Kind!.Value);

    /// <summary>The texts as one, as the compile reads them, and where each one is in it.</summary>
    private (string Text, List<Placed> Places) Joined()
    {
        if (_stack is null)
        {
            TextBuffer only = _bricks[0].Buffer;
            return (only.Text, [new Placed(_bricks[0], 0, only.Lines.Count)]);
        }

        string joined = ShaderStack.Join(_stack, Bricks);
        var places = new List<Placed>();
        foreach ((string label, int line) in SlangShaders.LineMarks(joined))
        {
            if (_bricks.Find(brick => LabelOf(brick) == label) is { } tab)
                places.Add(new Placed(tab, line - 1, tab.Buffer.Lines.Count));
        }

        return (joined, places);
    }

    /// <summary>The text a 1-based line of the whole is on, and its own 1-based line there; null on none (a mark between them).</summary>
    private static (Tab Tab, int Line)? Unjoin(List<Placed> places, int line)
    {
        foreach (Placed place in places)
        {
            if (line > place.Start && line <= place.Start + place.Count)
                return (place.Tab, line - place.Start);
        }

        return null;
    }

    /// <summary>Puts a text in the first tab and shows it.</summary>
    private void OpenBrick(Tab tab)
    {
        if (_open == 0)
        {
            if (Main == tab)
                return;
            Leave();
            _tabs[0] = tab;
            Load(tab);
        }
        else
        {
            _tabs[0] = tab;
            Show(0);
        }

        ListProblems();
    }

    /// <summary>
    /// Adds a brick from its template and opens it; compiled straight away. A brick it needs
    /// that needs it back comes with it; one it needs on its own is named in a toast.
    /// </summary>
    private void AddBrick(BrickKind kind)
    {
        List<BrickKind> missing = [.. ShaderStack.Needs(kind).Where(need => !_bricks.Exists(brick => brick.Kind == need))];
        List<BrickKind> together = [.. missing.Where(need => ShaderStack.Needs(need).Contains(kind))];
        Tab tab = NewBrick(new Brick(kind, ShaderStack.Template(kind)));
        _bricks.Add(tab);
        foreach (BrickKind need in together)
            _bricks.Add(NewBrick(new Brick(need, ShaderStack.Template(need))));
        _bricks.Sort((a, b) => a.Kind!.Value.CompareTo(b.Kind!.Value));
        Changed();
        OpenBrick(tab);

        List<BrickKind> lacking = [.. missing.Except(together)];
        if (lacking.Count > 0)
            Toasts?.ShowStatus($"{ShaderStack.Title(kind)} needs {string.Join(" and ", lacking.Select(ShaderStack.Title))}: add it from the strip");
    }

    /// <summary>Asks, then takes a brick out of the stack, opening the nearest one left.</summary>
    private void RemoveBrick(BrickKind kind)
    {
        if (_bricks.Find(brick => brick.Kind == kind) is not { } gone || _bricks.Count <= 1)
            return;
        _strip.Question = new EditorQuestion(
            $"Remove the {ShaderStack.Title(kind)} brick?", $"Its code goes from the stack; saving deletes {ShaderStack.FileName(kind)}.", "REMOVE", () =>
            {
                if (Main == gone)
                    OpenBrick(_bricks.Where(brick => brick != gone).MinBy(brick => Math.Abs(brick.Kind!.Value - kind))!);
                _bricks.Remove(gone);
                Changed();
                ListProblems();
                Focus();
            });
    }

    /// <summary>The bricks changed: everything that reads the whole reads it again, and it compiles now.</summary>
    private void Changed()
    {
        _structure++;
        _sinceEdit = Settle;
    }

    /// <summary>
    /// The list under the preview: the global problems, then the open text's, then the other
    /// bricks', each of those named by its brick.
    /// </summary>
    private void ListProblems()
    {
        var shown = new List<SlangProblem>(_global);
        var listed = new List<Tab?>(_global.Select(_ => (Tab?)null));
        foreach (Tab tab in _bricks.OrderBy(brick => brick != Main))
        {
            foreach (SlangProblem problem in tab.Problems)
            {
                shown.Add(tab == Main ? problem : problem with { Message = $"{ShaderStack.Title(tab.Kind!.Value)} · {problem.Message}" });
                listed.Add(tab);
            }
        }

        (_shownProblems, _listed) = (shown, listed);
    }

    /// <summary>The pipeline strip, following the bricks and their problems.</summary>
    private void DrawBricks() =>
        _brickStrip?.Draw(
            [.. ShaderStack.Kinds.Select(kind => _bricks.Find(brick => brick.Kind == kind) is { } tab
                ? new BrickLook(kind, true, tab.Problems.Count(problem => !problem.Warning), tab.Problems.Count(problem => problem.Warning))
                : new BrickLook(kind, false, 0, 0))],
            Main.Kind!.Value);

    /// <summary>A press on the pipeline strip: open, add or remove a brick. True when it took the press.</summary>
    private bool PressBricks(Vector2 point)
    {
        if (_brickStrip?.At(point, Main.Kind!.Value) is not (BrickPress press, BrickKind kind))
            return false;
        switch (press)
        {
            case BrickPress.Open:
                OpenBrick(_bricks.Find(brick => brick.Kind == kind)!);
                break;
            case BrickPress.Add:
                AddBrick(kind);
                break;
            case BrickPress.Remove:
                RemoveBrick(kind);
                break;
        }

        return true;
    }
}

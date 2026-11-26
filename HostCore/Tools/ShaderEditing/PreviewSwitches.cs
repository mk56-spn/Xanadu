using System.Collections.Generic;
using Xanadu.Core.Theme;
using Xanadu.Slang;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Editing;
using Xanadu.Ui.Ecs.Ui.Tree;
using static Xanadu.Ui.Ecs.Ui.Tree.UiSpecs;

namespace Xanadu.Host.Tools.ShaderEditing;

/// <summary>
/// The selected shape's switches (<see cref="SlangShapeSwitches"/>: MIRROR X, MIRROR Y, from the
/// library's flag tags) as a row of buttons along the bottom of the preview, on its right; bright
/// while on. A press writes the step into the code, one undo step. Hidden while no shape is selected.
/// </summary>
internal sealed class PreviewSwitches : IDisposable
{
    private enum SwitchCommand { Flip }

    private readonly UiNode _frame;
    private readonly IDisposable _presses;
    private readonly List<(string Method, IReadOnlyList<HandleTag> Flags, int Flag)> _switches = [];
    private readonly List<Button> _buttons = [];
    private UiNode _row;
    private ShapeChain? _selected;
    private TextBuffer? _buffer;
    private TextHistory? _history;
    private Action _done = () => { };

    public PreviewSwitches(UiNode frame)
    {
        _frame = frame;
        _presses = UiCommandEvents.OnPressed<SwitchCommand>(frame.Entity.Store, (_, press, _) => Flip(press.Arg));
    }

    /// <summary>Builds a button for every flag the library tags, in a row anchored to the preview's bottom right.</summary>
    public void Use(SlangHandleTags tags)
    {
        if (!_row.IsNull)
            UiLayout.DeleteSubtree(_row.Entity);
        _switches.Clear();
        _buttons.Clear();
        foreach ((string method, IReadOnlyList<HandleTag> flags) in tags.Switches())
        {
            for (int flag = 0; flag < flags.Count; flag++)
                _switches.Add((method, flags, flag));
        }

        var row = new Row(gap: UiMetrics.Space.Xs);
        for (int i = 0; i < _switches.Count; i++)
        {
            (string method, IReadOnlyList<HandleTag> flags, int flag) = _switches[i];
            string label = flags[flag].Label.Length > 0 ? flags[flag].Label : $"{method} {flags[flag].Parameter}".ToUpperInvariant();
            var button = new Button(label, UiType.Small, cmd: Cmd(SwitchCommand.Flip, i), tip: $"{method}({flags[flag].Parameter}) on the selected shape");
            _buttons.Add(button);
            row.Add(button);
        }

        _row = _frame.Add(row).Node.Anchor(UiEdge.BottomRight, UiMetrics.Space.Sm).Hidden();
    }

    /// <summary>Each frame: the selected shape (null hides the row) and each switch's state on it.</summary>
    public void Show(ShapeChain? selected, TextBuffer? buffer, TextHistory history, Action done)
    {
        (_selected, _buffer, _history, _done) = (selected, buffer, history, done);
        if (_row.IsNull)
            return;
        UiLayout.SetHidden(_row.Entity, selected is null);
        if (selected is null)
            return;
        for (int i = 0; i < _switches.Count; i++)
        {
            (string method, IReadOnlyList<HandleTag> flags, int flag) = _switches[i];
            _buttons[i].Node.Chosen(SlangShapeSwitches.IsOn(selected, method, flags, flag));
        }
    }

    private void Flip(int index)
    {
        if (_selected is not { } chain || _buffer is not { } buffer || index >= _switches.Count)
            return;
        (string method, IReadOnlyList<HandleTag> flags, int flag) = _switches[index];
        if (SlangShapeSwitches.Flip(chain, method, flags, flag) is not { } edit)
            return;
        (int line, int from) = TextPlaces.Place(buffer.Lines, edit.From);
        (int endLine, int to) = TextPlaces.Place(buffer.Lines, edit.To);
        if (endLine != line)
            return;
        _history?.Hold();
        buffer.Edit(line, from, to, edit.Text);
        _history?.LetGo();
        _done();
    }

    public void Dispose() => _presses.Dispose();
}

/// <summary>Offsets into lines joined by '\n', and back.</summary>
internal static class TextPlaces
{
    public static int Offset(IReadOnlyList<string> lines, int line, int column)
    {
        int offset = 0;
        for (int i = 0; i < line && i < lines.Count; i++)
            offset += lines[i].Length + 1;
        return offset + column;
    }

    public static (int Line, int Column) Place(IReadOnlyList<string> lines, int offset)
    {
        for (int line = 0; line < lines.Count; line++)
        {
            if (offset <= lines[line].Length)
                return (line, offset);
            offset -= lines[line].Length + 1;
        }

        return (lines.Count - 1, lines[^1].Length);
    }
}

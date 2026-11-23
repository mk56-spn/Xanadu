using System.Collections.Generic;
using System.Text;
using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Editing;
using Xanadu.Ui.Systems.Input;

namespace Xanadu.Ui.Systems.Ui;

/// <summary>A board's keys: typing into a box, and the keys that act on the selection.</summary>
public sealed partial class BoardInputSystem
{
    private const float RepeatDelay = 0.4f, RepeatEvery = 0.033f;

    private static readonly Key[] Digits =
        [Key.Key1, Key.Key2, Key.Key3, Key.Key4, Key.Key5, Key.Key6, Key.Key7, Key.Key8, Key.Key9];

    private static readonly Key[] Watched =
        [Key.Backspace, Key.Delete, Key.Enter, Key.KpEnter, Key.Escape, Key.F2, Key.V, Key.Y, Key.Z, .. Digits];

    private readonly HashSet<Key> _down = [], _pressed = [];
    private float _backspaceHeld, _backspaceNext;

    private void ReadKeys()
    {
        _pressed.Clear();
        foreach (Key key in Watched)
        {
            if (keyboard.IsPressed(key))
            {
                if (_down.Add(key))
                    _pressed.Add(key);
            }
            else
                _down.Remove(key);
        }
    }

    private bool Ctrl => keyboard.IsPressed(Key.Ctrl) || keyboard.IsPressed(Key.Meta);

    private bool Shift(PointerInput input) => input.Shift || keyboard.IsPressed(Key.Shift);

    private void Keys(Entity entity, ref UiBoard ui, PointerInput input, float delta)
    {
        BoardState state = ui.State;
        Board board = ui.Board;
        if (state.Editing != 0)
        {
            Type(entity, ref ui, input, delta);
            return;
        }

        // Whatever was typed before a box took the keyboard is not the box's.
        HostPlatform.TextEntry.TakeTyped();
        if (state.Gesture != BoardGestureKind.None)
            return;

        if (Ctrl && _pressed.Contains(Key.Z))
        {
            Step(entity, ref ui, Shift(input) ? state.Redo : state.Undo, Shift(input) ? state.Undo : state.Redo);
            return;
        }

        if (Ctrl && _pressed.Contains(Key.Y))
        {
            Step(entity, ref ui, state.Redo, state.Undo);
            return;
        }

        if (_pressed.Contains(Key.Delete) || _pressed.Contains(Key.Backspace))
        {
            Delete(entity, ref ui);
            return;
        }

        if ((_pressed.Contains(Key.Enter) || _pressed.Contains(Key.KpEnter) || _pressed.Contains(Key.F2)) &&
            state.Selected.Count == 1)
        {
            Edit(state, board, state.Selected.First());
            state.Before = board.Clone();
            return;
        }

        for (int i = 0; i < Digits.Length; i++)
        {
            if (!_pressed.Contains(Digits[i]) || state.Selected.Count == 0)
                continue;

            state.Before = board.Clone();
            bool changed = false;
            foreach (int id in state.Selected)
            {
                if (board.Find(id) is { } box && box.Category != i)
                {
                    box.Category = i;
                    changed = true;
                }
            }

            if (changed)
                Commit(entity, ref ui);
            else
                state.Before = null;
            return;
        }
    }

    private void Delete(Entity entity, ref UiBoard ui)
    {
        BoardState state = ui.State;
        Board board = ui.Board;
        if (state.Selected.Count == 0 && state.SelectedLink is null)
            return;

        state.Before = board.Clone();
        foreach (int id in state.Selected)
            board.Remove(id);
        if (state.SelectedLink is { } link)
            board.Links.Remove(link);
        state.Selected.Clear();
        state.SelectedLink = null;
        Commit(entity, ref ui);
    }

    /// <summary>Undo or redo: the board goes back to the last of <paramref name="from"/>, and what it was goes on <paramref name="to"/>.</summary>
    private void Step(Entity entity, ref UiBoard ui, List<Board> from, List<Board> to)
    {
        if (from.Count == 0)
            return;

        to.Add(ui.Board.Clone());
        ui.Board.CopyFrom(from[^1]);
        from.RemoveAt(from.Count - 1);
        Board board = ui.Board;
        ui.State.Selected.RemoveWhere(id => board.Find(id) is null);
        ui.State.SelectedLink = null;
        Changed(entity, ui);
    }

    private static void Edit(BoardState state, Board board, int id)
    {
        if (board.Find(id) is null)
            return;

        state.Editing = id;
        state.SinceCaretMove = 0;
        state.Gesture = BoardGestureKind.None;
        // Typing before this frame was meant for something else.
        HostPlatform.TextEntry.TakeTyped();
    }

    private void Type(Entity entity, ref UiBoard ui, PointerInput input, float delta)
    {
        BoardState state = ui.State;
        if (ui.Board.Find(state.Editing) is not { } box)
        {
            state.Editing = 0;
            return;
        }

        if (_pressed.Contains(Key.Escape) ||
            (!Shift(input) && (_pressed.Contains(Key.Enter) || _pressed.Contains(Key.KpEnter))))
        {
            Finish(entity, ref ui);
            return;
        }

        var text = new StringBuilder(box.Text);
        foreach (char c in HostPlatform.TextEntry.TakeTyped())
        {
            if (!char.IsControl(c))
                text.Append(c);
        }

        if (Ctrl && _pressed.Contains(Key.V))
            text.Append(HostPlatform.TextEntry.Clipboard.Replace("\r", ""));
        if (Shift(input) && (_pressed.Contains(Key.Enter) || _pressed.Contains(Key.KpEnter)))
            text.Append('\n');

        int erase = BackspaceSteps(delta);
        if (erase > 0)
            text.Length = System.Math.Max(0, text.Length - erase);

        string typed = text.ToString();
        if (typed != box.Text)
        {
            box.Text = typed;
            state.SinceCaretMove = 0;
        }
    }

    /// <summary>How many characters Backspace takes this frame: one on the press, then a run while held.</summary>
    private int BackspaceSteps(float delta)
    {
        if (!_down.Contains(Key.Backspace))
        {
            _backspaceHeld = 0;
            return 0;
        }

        if (_pressed.Contains(Key.Backspace))
        {
            _backspaceHeld = 0;
            _backspaceNext = RepeatDelay;
            return 1;
        }

        _backspaceHeld += delta;
        int steps = 0;
        while (_backspaceHeld >= _backspaceNext)
        {
            steps++;
            _backspaceNext += RepeatEvery;
        }

        return steps;
    }

    /// <summary>
    /// Stops typing into a box. A box made by this edit and left empty is taken
    /// back off; otherwise a changed text is an edit like any other.
    /// </summary>
    private void Finish(Entity entity, ref UiBoard ui)
    {
        BoardState state = ui.State;
        if (state.Editing == 0)
            return;

        int id = state.Editing;
        state.Editing = 0;
        Board? before = state.Before;
        BoardBox? box = ui.Board.Find(id);
        bool made = before?.Find(id) is null;
        if (box is not null && made && box.Text.Trim().Length == 0)
        {
            ui.Board.Remove(id);
            state.Selected.Remove(id);
            state.Before = null;
            return;
        }

        if (box is not null)
            box.Text = box.Text.TrimEnd();
        if (made || before?.Find(id)?.Text != box?.Text)
            Commit(entity, ref ui);
        else
            state.Before = null;
    }
}

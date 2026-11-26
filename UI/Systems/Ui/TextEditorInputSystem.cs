using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Editing;
using Xanadu.Ui.Systems.Input;

namespace Xanadu.Ui.Systems.Ui;

/// <summary>
/// Edits the focused <see cref="TextEditor"/>: typing, the caret keys (repeating
/// when held), Shift to select, Ctrl+A/C/X/V, Ctrl+Z/Y (and Ctrl+Shift+Z) for
/// undo, Enter keeping the indent, Tab to the next indent step; Esc, F2, F12 and
/// Ctrl+S go only to the owner. The pointer works
/// on every editor: a press focuses the one it lands in (and unfocuses the rest),
/// a click places the caret (with Ctrl, the owner may then go somewhere), a double click selects a word, a drag selects, and
/// the wheel scrolls. The owner's <see cref="ITextEditorHost"/> sees keys, typing
/// and presses first. A press in the gutter of a block, or on a folded one's pill, folds or opens it, as do
/// Ctrl+Shift+[ and ] for the block round the caret; Up, Down, the page keys and the wheel step over folded blocks.
/// </summary>
public sealed class TextEditorInputSystem(IPointerInputSource pointer, IKeyboardState keyboard, IWheelSource? wheel = null) : QuerySystem<TextEditor>
{
    /// <summary>A second click on the same place within this long selects the word.</summary>
    private const float DoubleClick = 0.4f;

    /// <summary>Lines one wheel notch scrolls.</summary>
    private const int WheelLines = 3;

    private static readonly Key[] Repeating =
    [
        Key.Left, Key.Right, Key.Up, Key.Down, Key.Pageup, Key.Pagedown,
        Key.Backspace, Key.Delete, Key.Enter, Key.KpEnter, Key.Tab,
    ];

    private static readonly Key[] Watched =
    [
        .. Repeating, Key.Home, Key.End, Key.Escape, Key.F2, Key.F12, Key.A, Key.C, Key.S, Key.V, Key.X, Key.Y, Key.Z, Key.Slash, Key.KpDivide,
        Key.Bracketleft, Key.Bracketright,
    ];

    /// <summary>Keys the editor itself does nothing with, passed to its owner once per press.</summary>
    private static readonly Key[] OwnersOnly = [Key.Escape, Key.F2, Key.F12];

    private readonly IPointerInputSource _pointer = pointer;
    private readonly IKeyboardState _keyboard = keyboard;
    private readonly IWheelSource? _wheel = wheel;
    private readonly HeldKeys _keys = new(keyboard, Watched);

    private bool _wasHeld;
    private bool _claimed;
    private float _clock;

    protected override void OnUpdate()
    {
        float delta = Tick.deltaTime;
        _clock += delta;
        _keys.Read();

        PointerInput input = _pointer.Read();
        bool pressed = input.Pressed && !_wasHeld;
        _wasHeld = input.Pressed;

        bool anyFocused = false;
        foreach (Entity entity in Query.ToEntityList())
        {
            ref TextEditor editor = ref entity.GetComponent<TextEditor>();
            TextEditors.Follow(entity, ref editor);
            TextBuffer buffer = editor.Buffer;
            int version = buffer.Version;
            (int, int) caret = (buffer.Line, buffer.Column);
            bool breaksRun = false, stepped = false, refused = false;

            bool focused = entity.Tags.Has<TextEditorFocused>();
            // Focus changes are tags, so they wait for the command buffer: a tag moves the component.
            if (Point(ref editor, input, pressed, ref breaksRun) is { } focus && focus != focused)
            {
                if (focus)
                    CommandBuffer.AddTag<TextEditorFocused>(entity.Id);
                else
                    CommandBuffer.RemoveTag<TextEditorFocused>(entity.Id);
            }

            if (focused)
            {
                anyFocused = true;
                Keys(ref editor, delta, ref breaksRun, ref stepped, ref refused);
            }

            if (!stepped)
                editor.History.After(typing: !breaksRun, delta);

            bool edited = buffer.Version != version;
            bool moved = caret != (buffer.Line, buffer.Column);
            editor.Frame = new TextEditorFrame(edited, moved, stepped, refused);
            if (edited || moved)
            {
                editor.SinceCaretMove = 0;
                editor.Layout.Reveal(buffer, editor.Host, buffer.Line);
            }
            else
                editor.SinceCaretMove += delta;
        }

        // Typing goes to an editor only while one has focus; text fields get it back after.
        if (anyFocused)
            HostPlatform.TextEntry.Claimed = _claimed = true;
        else if (_claimed)
            HostPlatform.TextEntry.Claimed = _claimed = false;
    }

    /// <summary>The pointer's part; returns whether a press focused (true) or unfocused (false) the editor.</summary>
    private bool? Point(ref TextEditor editor, PointerInput input, bool pressed, ref bool breaksRun)
    {
        TextBuffer buffer = editor.Buffer;
        TextEditorLayout layout = editor.Layout;
        if (!input.Pressed)
            editor.Dragging = false;

        if (pressed)
        {
            if (!layout.Area.HasPoint(input.Position))
                return false;

            if (editor.Host?.TakePress(buffer, input.Position) == true)
            {
                breaksRun = true;
                return true;
            }

            if (layout.FoldUnder(buffer, editor.Host, input.Position) is { } fold)
            {
                layout.ToggleFold(buffer, editor.Host, fold);
                breaksRun = true;
                return true;
            }

            if (FixIndent(editor, input.Position))
            {
                breaksRun = true;
                return true;
            }

            if (layout.Hit(buffer, editor.Host, input.Position) is { } hit)
            {
                bool quick = _clock - editor.LastClick < DoubleClick && editor.LastClickAt == hit;
                // A fourth click starts over rather than reselecting the line.
                editor.Clicks = quick && editor.Clicks < 3 ? editor.Clicks + 1 : 1;
                switch (editor.Clicks)
                {
                    case 3:
                        buffer.SelectLine(hit.Line);
                        break;
                    case 2:
                        buffer.SelectWord(hit.Line, hit.Column);
                        break;
                    default:
                        buffer.Place(hit.Line, hit.Column, extend: input.Shift || _keyboard.IsPressed(Key.Shift));
                        break;
                }

                editor.LastClick = _clock;
                editor.LastClickAt = hit;
                editor.Dragging = true;
                bool ctrl = input.Ctrl || _keyboard.IsPressed(Key.Ctrl) || _keyboard.IsPressed(Key.Meta);
                if (editor.Clicks == 1 && ctrl && editor.Host?.TakeCtrlClick(buffer) == true)
                {
                    editor.Dragging = false;
                    editor.LastClick = -1;
                }
            }

            Scroll(ref editor, input);
            return true;
        }
        else if (editor.Dragging && layout.Hit(buffer, editor.Host, input.Position, dragging: true) is { } to)
        {
            // After a double or triple click the drag grows by whole words or lines.
            if (editor.Clicks > 1)
                buffer.SelectUnits(editor.LastClickAt, to, lines: editor.Clicks == 3);
            else if (to != (buffer.Line, buffer.Column))
                buffer.Place(to.Line, to.Column, extend: true);
        }

        Scroll(ref editor, input);
        return null;
    }

    /// <summary>A press on the marked indent of a line off the steps moves its text to where it belongs.</summary>
    private static bool FixIndent(in TextEditor editor, Vector2 point)
    {
        TextBuffer buffer = editor.Buffer;
        TextEditorLayout layout = editor.Layout;
        if (layout.Hit(buffer, editor.Host, point) is not { } hit)
            return false;
        string line = buffer.Lines[hit.Line];
        if (!TextIndent.Misaligned(line) || point.X >= layout.TextLeft + TextIndent.Of(line) * layout.CharWidth)
            return false;
        int wanted = editor.Host.WantedIndent(buffer, hit.Line);
        buffer.Reindent(hit.Line, wanted);
        buffer.Place(hit.Line, wanted);
        return true;
    }

    private void Scroll(ref TextEditor editor, PointerInput input)
    {
        TextEditorLayout layout = editor.Layout;
        if (_wheel is { Notches: not 0 } && layout.Area.HasPoint(input.Position))
        {
            int notches = _wheel.TakeWheel();
            layout.Scroll = layout.Folds(editor.Buffer, editor.Host).Step(layout.Scroll, -notches * WheelLines, editor.Buffer.Lines.Count);
        }
    }

    private void Keys(ref TextEditor editor, float delta, ref bool breaksRun, ref bool stepped, ref bool refused)
    {
        TextBuffer buffer = editor.Buffer;
        ITextEditorHost? host = editor.Host;
        bool ctrl = _keyboard.IsPressed(Key.Ctrl) || _keyboard.IsPressed(Key.Meta);
        bool shift = _keyboard.IsPressed(Key.Shift);
        ITextEntry entry = HostPlatform.TextEntry;
        string typed = entry.Claimed ? entry.TakeTyped() : "";

        // Ctrl+Shift+[ folds the block round the caret, ] opens it; a read-only text folds too.
        if (ctrl && shift && (_keys.Pressed.Contains(Key.Bracketleft) || _keys.Pressed.Contains(Key.Bracketright))
            && editor.Layout.Folds(buffer, host) is var folds && folds.BlockAt(buffer.Line) is var block and >= 0
            && folds.IsFolded(block) != _keys.Pressed.Contains(Key.Bracketleft))
            editor.Layout.ToggleFold(buffer, host, block);

        if (editor.ReadOnly)
        {
            ReadOnlyKeys(ref editor, delta, typed, ctrl, shift, ref refused);
            return;
        }

        // Undo and redo take the whole frame, so nothing else edits around them.
        if (ctrl && (_keys.Pressed.Contains(Key.Z) || _keys.Pressed.Contains(Key.Y)))
        {
            if (_keys.Pressed.Contains(Key.Z) && !shift)
                editor.History.Undo();
            else
                editor.History.Redo();
            stepped = true;
            return;
        }

        // Anything typed over a selection replaces it: its own step, not part of a run.
        breaksRun = buffer.HasSelection;

        if (typed.Length > 0 && !ctrl && host?.TakeTyped(buffer, typed) != true)
            buffer.Insert(typed);
        if (ctrl && _keys.Pressed.Contains(Key.A))
            buffer.SelectAll();
        if (ctrl && (_keys.Pressed.Contains(Key.C) || _keys.Pressed.Contains(Key.X)) && buffer.HasSelection)
        {
            entry.Clipboard = buffer.SelectedText;
            if (_keys.Pressed.Contains(Key.X))
            {
                buffer.DeleteSelection();
                breaksRun = true;
            }
        }

        // Ctrl+/: comment the lines out, or back in, when the text has line comments.
        if (ctrl && (_keys.Pressed.Contains(Key.Slash) || _keys.Pressed.Contains(Key.KpDivide)) && host?.LineComment is { Length: > 0 } marker)
        {
            buffer.ToggleComment(marker);
            breaksRun = true;
        }

        if (ctrl && _keys.Pressed.Contains(Key.V) && entry.Clipboard is { Length: > 0 } clip)
        {
            buffer.Insert(clip);
            breaksRun = true;
        }

        // Saving is the owner's: the editor only holds text.
        if (ctrl && _keys.Pressed.Contains(Key.S))
            breaksRun |= host?.TakeKey(buffer, Key.S, shift, ctrl) == true;

        // Keys only an owner has a use for (F2: rename, F12: go to a declaration in a code editor).
        foreach (Key key in OwnersOnly)
        {
            if (_keys.Pressed.Contains(key))
                breaksRun |= host?.TakeKey(buffer, key, shift, ctrl) == true;
        }

        foreach (Key key in Repeating)
        {
            if (!_keys.Fires(key, delta))
                continue;
            int version = buffer.Version;
            if (host?.TakeKey(buffer, key, shift, ctrl) == true)
            {
                breaksRun |= buffer.Version != version;
                continue;
            }

            breaksRun |= Press(editor, key, shift, ctrl);
        }

        if (_keys.Pressed.Contains(Key.Home))
            buffer.Home(shift);
        if (_keys.Pressed.Contains(Key.End))
            buffer.End(shift);
    }

    /// <summary>Keys for a read-only editor: moves, selecting, copying and the owner's keys go ahead; anything that edits is refused.</summary>
    private void ReadOnlyKeys(ref TextEditor editor, float delta, string typed, bool ctrl, bool shift, ref bool refused)
    {
        TextBuffer buffer = editor.Buffer;
        ITextEditorHost? host = editor.Host;
        refused |= typed.Length > 0 && !ctrl;
        refused |= ctrl && (_keys.Pressed.Contains(Key.Z) || _keys.Pressed.Contains(Key.Y) || _keys.Pressed.Contains(Key.X) || _keys.Pressed.Contains(Key.V)
                            || _keys.Pressed.Contains(Key.Slash) || _keys.Pressed.Contains(Key.KpDivide));
        if (ctrl && _keys.Pressed.Contains(Key.A))
            buffer.SelectAll();
        if (ctrl && _keys.Pressed.Contains(Key.C) && buffer.HasSelection)
            HostPlatform.TextEntry.Clipboard = buffer.SelectedText;
        if (ctrl && _keys.Pressed.Contains(Key.S))
            host?.TakeKey(buffer, Key.S, shift, ctrl);
        foreach (Key key in OwnersOnly)
        {
            if (_keys.Pressed.Contains(key))
                host?.TakeKey(buffer, key, shift, ctrl);
        }

        foreach (Key key in Repeating)
        {
            if (!_keys.Fires(key, delta))
                continue;
            if (key is Key.Backspace or Key.Delete or Key.Enter or Key.KpEnter or Key.Tab)
                refused = true;
            else
                Press(editor, key, shift, ctrl);
        }

        if (_keys.Pressed.Contains(Key.Home))
            buffer.Home(shift);
        if (_keys.Pressed.Contains(Key.End))
            buffer.End(shift);
    }

    /// <summary>
    /// A key's edit or move; with Shift held, moves extend the selection, with Ctrl Left, Right,
    /// Backspace and Delete go a word at a time. True for an edit that is its own undo step.
    /// </summary>
    private static bool Press(in TextEditor editor, Key key, bool shift, bool ctrl)
    {
        TextBuffer buffer = editor.Buffer;
        switch (key)
        {
            case Key.Left when ctrl: buffer.MoveWord(-1, shift); break;
            case Key.Right when ctrl: buffer.MoveWord(1, shift); break;
            case Key.Backspace when ctrl: buffer.DeleteWord(-1); return true;
            case Key.Delete when ctrl: buffer.DeleteWord(1); return true;
            case Key.Left: buffer.MoveColumn(-1, shift); break;
            case Key.Right: buffer.MoveColumn(1, shift); break;
            case Key.Up or Key.Down or Key.Pageup or Key.Pagedown:
                int rows = key is Key.Up or Key.Down ? 1 : editor.Layout.VisibleRows;
                int to = editor.Layout.Folds(buffer, editor.Host).Step(buffer.Line, key is Key.Up or Key.Pageup ? -rows : rows, buffer.Lines.Count);
                buffer.MoveLine(to - buffer.Line, shift);
                break;
            case Key.Backspace: buffer.Backspace(); break;
            case Key.Delete: buffer.Delete(); break;
            case Key.Enter or Key.KpEnter:
                buffer.DeleteSelection();
                buffer.NewLine(indent: editor.Host.WantedIndent(buffer, buffer.Line));
                return true;
            case Key.Tab when shift:
                buffer.OutdentLines();
                return true;
            case Key.Tab when buffer.HasSelection:
                buffer.IndentLines();
                return true;
            case Key.Tab: buffer.IndentLines(); break;
        }

        return false;
    }
}

using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Systems.Input;
using Xanadu.Ui.Editing;

namespace Xanadu.Systems.Ui;

/// <summary>
/// Edits the focused <see cref="TextEditor"/>: typing, the caret keys (repeating
/// when held), Shift to select, Ctrl+A/C/X/V, Ctrl+Z/Y (and Ctrl+Shift+Z) for
/// undo, Enter keeping the indent, Tab to the next indent step; Esc and F2 go
/// only to the owner. The pointer works
/// on every editor: a press focuses the one it lands in (and unfocuses the rest),
/// a click places the caret, a double click selects a word, a drag selects, and
/// the wheel scrolls. The owner's <see cref="ITextEditorHost"/> sees keys, typing
/// and presses first.
/// </summary>
public sealed class TextEditorInputSystem : QuerySystem<TextEditor>
{
    private const float RepeatDelay = 0.4f, RepeatEvery = 0.033f;

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
        .. Repeating, Key.Home, Key.End, Key.Escape, Key.F2, Key.A, Key.C, Key.V, Key.X, Key.Y, Key.Z,
    ];

    /// <summary>Keys the editor itself does nothing with, passed to its owner once per press.</summary>
    private static readonly Key[] OwnersOnly = [Key.Escape, Key.F2];

    private readonly IPointerInputSource _pointer;
    private readonly IKeyboardState _keyboard;
    private readonly IWheelSource? _wheel;

    /// <summary>Keys down last frame, for presses; how long each repeating key has been held.</summary>
    private readonly HashSet<Key> _down = new();
    private readonly HashSet<Key> _pressed = new();
    private readonly Dictionary<Key, float> _held = new();

    private bool _wasHeld;
    private bool _claimed;
    private float _clock;

    public TextEditorInputSystem(IPointerInputSource pointer, IKeyboardState keyboard, IWheelSource? wheel = null)
    {
        _pointer = pointer;
        _keyboard = keyboard;
        _wheel = wheel;
    }

    protected override void OnUpdate()
    {
        float delta = Tick.deltaTime;
        _clock += delta;
        ReadKeys();

        PointerInput input = _pointer.Read();
        bool pressed = input.Pressed && !_wasHeld;
        _wasHeld = input.Pressed;

        bool anyFocused = false;
        foreach (Entity entity in Query.ToEntityList())
        {
            ref TextEditor editor = ref entity.GetComponent<TextEditor>();
            TextBuffer buffer = editor.Buffer;
            int version = buffer.Version;
            (int, int) caret = (buffer.Line, buffer.Column);
            bool breaksRun = false, stepped = false;

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
                Keys(ref editor, delta, ref breaksRun, ref stepped);
            }

            if (!stepped)
                editor.History.After(typing: !breaksRun, delta);

            bool edited = buffer.Version != version;
            bool moved = caret != (buffer.Line, buffer.Column);
            editor.Frame = new TextEditorFrame(edited, moved, stepped);
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

    private void ReadKeys()
    {
        _pressed.Clear();
        foreach (Key key in Watched)
        {
            if (_keyboard.IsPressed(key))
            {
                if (_down.Add(key))
                    _pressed.Add(key);
            }
            else
                _down.Remove(key);
        }
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

            if (layout.Hit(buffer, editor.Host, input.Position) is { } hit)
            {
                bool twice = _clock - editor.LastClick < DoubleClick && editor.LastClickAt == hit;
                if (twice)
                    buffer.SelectWord(hit.Line, hit.Column);
                else
                    buffer.Place(hit.Line, hit.Column, extend: input.Shift || _keyboard.IsPressed(Key.Shift));
                // A third click starts over rather than reselecting the word.
                editor.LastClick = twice ? -1 : _clock;
                editor.LastClickAt = hit;
                editor.Dragging = !twice;
            }

            Scroll(ref editor, input);
            return true;
        }
        else if (editor.Dragging && layout.Hit(buffer, editor.Host, input.Position, dragging: true) is { } to
                 && to != (buffer.Line, buffer.Column))
            buffer.Place(to.Line, to.Column, extend: true);

        Scroll(ref editor, input);
        return null;
    }

    private void Scroll(ref TextEditor editor, PointerInput input)
    {
        TextEditorLayout layout = editor.Layout;
        if (_wheel is { Notches: not 0 } && layout.Area.HasPoint(input.Position))
        {
            int notches = _wheel.TakeWheel();
            layout.Scroll = Math.Clamp(layout.Scroll - notches * WheelLines, 0, Math.Max(0, editor.Buffer.Lines.Count - 1));
        }
    }

    private void Keys(ref TextEditor editor, float delta, ref bool breaksRun, ref bool stepped)
    {
        TextBuffer buffer = editor.Buffer;
        ITextEditorHost? host = editor.Host;
        bool ctrl = _keyboard.IsPressed(Key.Ctrl) || _keyboard.IsPressed(Key.Meta);
        bool shift = _keyboard.IsPressed(Key.Shift);
        ITextEntry entry = HostPlatform.TextEntry;
        string typed = entry.Claimed ? entry.TakeTyped() : "";

        // Undo and redo take the whole frame, so nothing else edits around them.
        if (ctrl && (_pressed.Contains(Key.Z) || _pressed.Contains(Key.Y)))
        {
            if (_pressed.Contains(Key.Z) && !shift)
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
        if (ctrl && _pressed.Contains(Key.A))
            buffer.SelectAll();
        if (ctrl && (_pressed.Contains(Key.C) || _pressed.Contains(Key.X)) && buffer.HasSelection)
        {
            entry.Clipboard = buffer.SelectedText;
            if (_pressed.Contains(Key.X))
            {
                buffer.DeleteSelection();
                breaksRun = true;
            }
        }

        if (ctrl && _pressed.Contains(Key.V) && entry.Clipboard is { Length: > 0 } clip)
        {
            buffer.Insert(clip);
            breaksRun = true;
        }

        // Keys only an owner has a use for (F2: rename in a code editor).
        foreach (Key key in OwnersOnly)
        {
            if (_pressed.Contains(key))
                breaksRun |= host?.TakeKey(buffer, key, shift, ctrl) == true;
        }

        foreach (Key key in Repeating)
        {
            if (!Fires(key, delta))
                continue;
            int version = buffer.Version;
            if (host?.TakeKey(buffer, key, shift, ctrl) == true)
            {
                breaksRun |= buffer.Version != version;
                continue;
            }

            breaksRun |= Press(editor, key, shift);
        }

        if (_pressed.Contains(Key.Home))
            buffer.Home(shift);
        if (_pressed.Contains(Key.End))
            buffer.End(shift);
    }

    /// <summary>A key's edit or move; with Shift held, moves extend the selection. True for an edit that is its own undo step.</summary>
    private static bool Press(in TextEditor editor, Key key, bool shift)
    {
        TextBuffer buffer = editor.Buffer;
        switch (key)
        {
            case Key.Left: buffer.MoveColumn(-1, shift); break;
            case Key.Right: buffer.MoveColumn(1, shift); break;
            case Key.Up: buffer.MoveLine(-1, shift); break;
            case Key.Down: buffer.MoveLine(1, shift); break;
            case Key.Pageup: buffer.MoveLine(-editor.Layout.VisibleRows, shift); break;
            case Key.Pagedown: buffer.MoveLine(editor.Layout.VisibleRows, shift); break;
            case Key.Backspace: buffer.Backspace(); break;
            case Key.Delete: buffer.Delete(); break;
            case Key.Enter or Key.KpEnter:
                buffer.NewLine();
                return true;
            case Key.Tab: buffer.Insert(new string(' ', TextBuffer.Indent - buffer.Column % TextBuffer.Indent)); break;
        }

        return false;
    }

    /// <summary>A key fires when pressed, then repeatedly once held past a delay.</summary>
    private bool Fires(Key key, float delta)
    {
        if (_pressed.Contains(key))
        {
            _held[key] = 0;
            return true;
        }

        if (!_down.Contains(key) || !_held.TryGetValue(key, out float before))
        {
            _held.Remove(key);
            return false;
        }

        float now = before + delta;
        _held[key] = now;
        if (now < RepeatDelay)
            return false;
        return before < RepeatDelay || (int)((before - RepeatDelay) / RepeatEvery) != (int)((now - RepeatDelay) / RepeatEvery);
    }
}

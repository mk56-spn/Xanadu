using Xanadu.Core.History;

namespace Xanadu.Ui.Editing;

/// <summary>
/// Undo and redo for a <see cref="TextBuffer"/>, on the shared <see cref="UndoHistory{T}"/>
/// the level editor and the animator use. It watches the buffer once a frame
/// rather than wrapping every edit: plain typing (characters, Backspace,
/// Delete, Tab with nothing selected) runs together into one step until the
/// caret moves on its own or typing pauses; any other edit (Enter, paste, cut,
/// replacing a selection, anything an owner does to the text) is a step of its own.
/// An owner changing the text over many frames (a drag) holds the history
/// (<see cref="Hold"/>, <see cref="LetGo"/>): everything in between is one step.
/// </summary>
public sealed class TextHistory(TextBuffer buffer)
{
    /// <summary>A pause this long ends a run of typing.</summary>
    private const float RunPause = 1f;

    private readonly TextBuffer _buffer = buffer;
    private readonly UndoHistory<TextSnapshot> _history = new UndoHistory<TextSnapshot>((a, b) => a.Text == b.Text, buffer.Restore);

    /// <summary>The buffer as it was at the end of the last frame (text from the last change, caret kept current).</summary>
    private TextSnapshot _latest = buffer.Snapshot();

    /// <summary>Where the open run of typing began; null when none is open.</summary>
    private TextSnapshot? _runStart;

    private int _seenVersion = buffer.Version;
    private float _idle;

    /// <summary>Held: every change runs into one step until <see cref="LetGo"/>.</summary>
    private bool _held;

    /// <summary>Let go since the last frame: the held step closes once the last change is seen.</summary>
    private bool _closing;

    /// <summary>Called once a frame after the input is handled; <paramref name="typing"/> says the frame's edits were plain typing.</summary>
    public void After(bool typing, float delta)
    {
        if (_buffer.Version == _seenVersion)
        {
            bool caretMoved = (_buffer.Line, _buffer.Column, _buffer.Anchor) != (_latest.Line, _latest.Column, _latest.Anchor);
            _idle += delta;
            if (_runStart is not null && (_closing || (!_held && (caretMoved || _idle >= RunPause))))
                CloseRun();
            _closing = false;
            if (caretMoved)
                _latest = _latest with { Line = _buffer.Line, Column = _buffer.Column, Anchor = _buffer.Anchor };
            return;
        }

        TextSnapshot now = _buffer.Snapshot();
        if (typing || _held || _closing)
            _runStart ??= _latest;
        else
        {
            CloseRun();
            _history.Record(_latest, now);
        }

        _idle = 0;
        _latest = now;
        _seenVersion = _buffer.Version;
        if (_closing)
        {
            CloseRun();
            _closing = false;
        }
    }

    /// <summary>Starts one step that every change joins, whatever makes it, until <see cref="LetGo"/>; typing before it is a step of its own.</summary>
    public void Hold()
    {
        if (_held)
            return;
        CloseRun();
        _held = true;
    }

    /// <summary>Ends what <see cref="Hold"/> started; the next <see cref="After"/> closes the step, a change made this frame included.</summary>
    public void LetGo()
    {
        if (!_held)
            return;
        _held = false;
        _closing = true;
    }

    public bool Undo() => Step(undo: true);

    public bool Redo() => Step(undo: false);

    private bool Step(bool undo)
    {
        CloseRun();
        bool done = undo ? _history.Undo() : _history.Redo();
        _latest = _buffer.Snapshot();
        _seenVersion = _buffer.Version;
        return done;
    }

    private void CloseRun()
    {
        if (_runStart is { } start)
            _history.Record(start, _latest);
        _runStart = null;
    }
}

using Xanadu.History;

namespace Xanadu.Ui.Editing;

/// <summary>
/// Undo and redo for a <see cref="TextBuffer"/>, on the shared <see cref="UndoHistory{T}"/>
/// the level editor and the animator use. It watches the buffer once a frame
/// rather than wrapping every edit: plain typing (characters, Backspace,
/// Delete, Tab with nothing selected) runs together into one step until the
/// caret moves on its own or typing pauses; any other edit (Enter, paste, cut,
/// replacing a selection, anything an owner does to the text) is a step of its own.
/// </summary>
public sealed class TextHistory
{
    /// <summary>A pause this long ends a run of typing.</summary>
    private const float RunPause = 1f;

    private readonly TextBuffer _buffer;
    private readonly UndoHistory<TextSnapshot> _history;

    /// <summary>The buffer as it was at the end of the last frame (text from the last change, caret kept current).</summary>
    private TextSnapshot _latest;

    /// <summary>Where the open run of typing began; null when none is open.</summary>
    private TextSnapshot? _runStart;

    private int _seenVersion;
    private float _idle;

    public TextHistory(TextBuffer buffer)
    {
        _buffer = buffer;
        // Caret-only differences are not steps.
        _history = new UndoHistory<TextSnapshot>((a, b) => a.Text == b.Text, buffer.Restore);
        _latest = buffer.Snapshot();
        _seenVersion = buffer.Version;
    }

    /// <summary>Called once a frame after the input is handled; <paramref name="typing"/> says the frame's edits were plain typing.</summary>
    public void After(bool typing, float delta)
    {
        if (_buffer.Version == _seenVersion)
        {
            bool caretMoved = (_buffer.Line, _buffer.Column, _buffer.Anchor) != (_latest.Line, _latest.Column, _latest.Anchor);
            _idle += delta;
            if (_runStart is not null && (caretMoved || _idle >= RunPause))
                CloseRun();
            if (caretMoved)
                _latest = _latest with { Line = _buffer.Line, Column = _buffer.Column, Anchor = _buffer.Anchor };
            return;
        }

        TextSnapshot now = _buffer.Snapshot();
        if (typing)
            _runStart ??= _latest;
        else
        {
            CloseRun();
            _history.Record(_latest, now);
        }

        _idle = 0;
        _latest = now;
        _seenVersion = _buffer.Version;
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

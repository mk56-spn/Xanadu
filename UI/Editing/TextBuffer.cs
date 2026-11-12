using System;
using System.Collections.Generic;

namespace Xanadu.Ui.Editing;

/// <summary>
/// Text as lines, with one caret and the editing any text editor needs: typing,
/// new lines that keep the indentation,
/// Backspace that removes a whole indent step, Delete, and caret moves that
/// remember the column when passing short lines; word moves and word deletes,
/// indenting and outdenting whole lines, and turning lines into comments and back. A selection runs from an
/// anchor to the caret; moves that extend keep the anchor, other moves drop it,
/// and any edit replaces what is selected.
/// </summary>
public sealed class TextBuffer
{
    public const int Indent = 4;

    private readonly List<string> _lines = [""];

    /// <summary>The column up and down try to keep, so passing a short line doesn't lose it.</summary>
    private int _wantedColumn;

    public IReadOnlyList<string> Lines => _lines;

    public int Line { get; private set; }

    public int Column { get; private set; }

    /// <summary>Where the selection started; the caret is its other end. Null when nothing is selected.</summary>
    public (int Line, int Column)? Anchor { get; private set; }

    public bool HasSelection => Anchor is { } anchor && anchor != (Line, Column);

    /// <summary>The selection's ends in text order.</summary>
    public ((int Line, int Column) Start, (int Line, int Column) End) Selection
    {
        get
        {
            (int, int) caret = (Line, Column), anchor = Anchor ?? caret;
            return Before(anchor, caret) ? (anchor, caret) : (caret, anchor);
        }
    }

    public string SelectedText
    {
        get
        {
            if (!HasSelection)
                return "";
            var (start, end) = Selection;
            if (start.Line == end.Line)
                return _lines[start.Line][start.Column..end.Column];
            var parts = new List<string> { _lines[start.Line][start.Column..] };
            for (int i = start.Line + 1; i < end.Line; i++)
                parts.Add(_lines[i]);
            parts.Add(_lines[end.Line][..end.Column]);
            return string.Join('\n', parts);
        }
    }

    /// <summary>Goes up on every change to the text (not on caret moves).</summary>
    public int Version { get; private set; }

    public string Text => string.Join('\n', _lines);

    public void Load(string text)
    {
        _lines.Clear();
        _lines.AddRange(text.ReplaceLineEndings("\n").Replace("\t", new string(' ', Indent)).Split('\n'));
        Line = 0;
        Column = 0;
        Anchor = null;
        _wantedColumn = 0;
        Version++;
    }

    /// <summary>The text, caret and selection as they are now, for undo.</summary>
    public TextSnapshot Snapshot() => new(Text, Line, Column, Anchor);

    /// <summary>Puts back a snapshot's text, caret and selection (an undo or redo).</summary>
    public void Restore(TextSnapshot snapshot)
    {
        _lines.Clear();
        _lines.AddRange(snapshot.Text.Split('\n'));
        Line = Math.Clamp(snapshot.Line, 0, _lines.Count - 1);
        Column = Math.Clamp(snapshot.Column, 0, _lines[Line].Length);
        Anchor = snapshot.Anchor;
        _wantedColumn = Column;
        Version++;
    }

    public void SelectAll()
    {
        Anchor = (0, 0);
        Line = _lines.Count - 1;
        Column = _lines[Line].Length;
        _wantedColumn = Column;
    }

    /// <summary>Selects the word (or run of spaces, or single symbol) at a place: a double click.</summary>
    public void SelectWord(int line, int column)
    {
        Place(line, column);
        string text = _lines[Line];
        if (text.Length == 0)
            return;
        int at = Math.Min(Column, text.Length - 1);
        // Just past the end of a word, the word is meant.
        if (Column == text.Length || (Column > 0 && Kind(text[at]) != Kind(text[Column - 1]) && Kind(text[Column - 1]) == 0))
            at = Math.Max(0, Column - 1);
        int kind = Kind(text[at]), start = at, end = at + 1;
        if (kind != 2)
        {
            while (start > 0 && Kind(text[start - 1]) == kind)
                start--;
            while (end < text.Length && Kind(text[end]) == kind)
                end++;
        }

        Anchor = (Line, start);
        Column = end;
        _wantedColumn = Column;
    }

    /// <summary>Removes the selection, leaving the caret where it began; false when nothing was selected.</summary>
    public bool DeleteSelection()
    {
        if (!HasSelection)
        {
            Anchor = null;
            return false;
        }

        var (start, end) = Selection;
        _lines[start.Line] = _lines[start.Line][..start.Column] + _lines[end.Line][end.Column..];
        _lines.RemoveRange(start.Line + 1, end.Line - start.Line);
        Line = start.Line;
        Column = start.Column;
        Anchor = null;
        _wantedColumn = Column;
        Version++;
        return true;
    }

    /// <summary>Types text at the caret; new lines in it split the line, tabs become spaces.</summary>
    public void Insert(string text)
    {
        DeleteSelection();
        if (text.Length == 0)
            return;

        string[] parts = text.ReplaceLineEndings("\n").Replace("\t", new string(' ', Indent)).Split('\n');
        string line = _lines[Line];
        string before = line[..Column], after = line[Column..];
        if (parts.Length == 1)
        {
            _lines[Line] = before + parts[0] + after;
            Column = before.Length + parts[0].Length;
        }
        else
        {
            _lines[Line] = before + parts[0];
            for (int i = 1; i < parts.Length; i++)
                _lines.Insert(Line + i, parts[i] + (i == parts.Length - 1 ? after : ""));
            Line += parts.Length - 1;
            Column = parts[^1].Length;
        }

        _wantedColumn = Column;
        Version++;
    }

    /// <summary>Enter: splits the line, the new one indented like this one plus <paramref name="extraIndent"/> spaces.</summary>
    public void NewLine(int extraIndent = 0)
    {
        DeleteSelection();
        string line = _lines[Line];
        int indent = line.Length - line.TrimStart(' ').Length + extraIndent;
        string after = line[Column..].TrimStart(' ');
        _lines[Line] = line[..Column].TrimEnd(' ');
        _lines.Insert(Line + 1, new string(' ', indent) + after);
        Line++;
        Column = indent;
        _wantedColumn = Column;
        Version++;
    }

    /// <summary>Removes what is before the caret: a whole indent step inside leading spaces, or joins with the line above.</summary>
    public void Backspace()
    {
        if (DeleteSelection())
            return;
        string line = _lines[Line];
        if (Column == 0)
        {
            if (Line == 0)
                return;
            Column = _lines[Line - 1].Length;
            _lines[Line - 1] += line;
            _lines.RemoveAt(Line);
            Line--;
        }
        else
        {
            int remove = 1;
            if (line[..Column].Trim().Length == 0)
                remove = (Column - 1) % Indent + 1;
            _lines[Line] = line[..(Column - remove)] + line[Column..];
            Column -= remove;
        }

        _wantedColumn = Column;
        Version++;
    }

    /// <summary>Removes what is after the caret, joining the next line at the end of this one.</summary>
    public void Delete()
    {
        if (DeleteSelection())
            return;
        string line = _lines[Line];
        if (Column < line.Length)
            _lines[Line] = line[..Column] + line[(Column + 1)..];
        else if (Line + 1 < _lines.Count)
        {
            _lines[Line] = line + _lines[Line + 1];
            _lines.RemoveAt(Line + 1);
        }
        else
            return;

        Version++;
    }

    /// <summary>Left or right by characters, crossing line ends. Without extending, a selection collapses to its side instead.</summary>
    public void MoveColumn(int by, bool extend = false)
    {
        if (!extend && HasSelection)
        {
            var (start, end) = Selection;
            (Line, Column) = by < 0 ? start : end;
            Anchor = null;
            _wantedColumn = Column;
            return;
        }

        Extend(extend);
        int column = Column + by;
        int line = Line;
        while (column < 0 && line > 0)
        {
            line--;
            column += _lines[line].Length + 1;
        }

        while (column > _lines[line].Length && line + 1 < _lines.Count)
        {
            column -= _lines[line].Length + 1;
            line++;
        }

        Line = line;
        Column = Math.Clamp(column, 0, _lines[line].Length);
        _wantedColumn = Column;
    }

    /// <summary>Up or down by lines, keeping the column the caret had before short lines got in the way.</summary>
    public void MoveLine(int by, bool extend = false)
    {
        Extend(extend);
        Line = Math.Clamp(Line + by, 0, _lines.Count - 1);
        Column = Math.Min(_wantedColumn, _lines[Line].Length);
    }

    /// <summary>Home: to the first non-space character, or to column 0 if already there.</summary>
    public void Home(bool extend = false)
    {
        Extend(extend);
        string line = _lines[Line];
        int first = line.Length - line.TrimStart(' ').Length;
        Column = Column == first ? 0 : first;
        _wantedColumn = Column;
    }

    public void End(bool extend = false)
    {
        Extend(extend);
        Column = _lines[Line].Length;
        _wantedColumn = Column;
    }

    /// <summary>Replaces columns [from, to) of the caret's line with text and puts the caret after it (a completion).</summary>
    public void Replace(int from, int to, string text)
    {
        string line = _lines[Line];
        from = Math.Clamp(from, 0, line.Length);
        to = Math.Clamp(to, from, line.Length);
        _lines[Line] = line[..from] + text + line[to..];
        Anchor = null;
        Column = from + text.Length;
        _wantedColumn = Column;
        Version++;
    }

    /// <summary>
    /// Replaces columns [from, to) of any line, the caret kept on the same text: after the
    /// change it moves with it, inside it it goes to its end. Drops the selection (a rename's other places).
    /// </summary>
    public void Edit(int line, int from, int to, string text)
    {
        string old = _lines[line];
        from = Math.Clamp(from, 0, old.Length);
        to = Math.Clamp(to, from, old.Length);
        _lines[line] = old[..from] + text + old[to..];
        Anchor = null;
        if (Line == line && Column >= to)
            Column += text.Length - (to - from);
        else if (Line == line && Column > from)
            Column = from + text.Length;
        _wantedColumn = Column;
        Version++;
    }

    /// <summary>
    /// The lines an edit of whole lines covers: the selection's, or the caret's. A selection ending at
    /// the very start of a line leaves that line out (dragging down to just below a block).
    /// </summary>
    private (int First, int Last) SelectedLines()
    {
        if (!HasSelection)
            return (Line, Line);
        var (start, end) = Selection;
        return (start.Line, end.Column == 0 && end.Line > start.Line ? end.Line - 1 : end.Line);
    }

    /// <summary>
    /// Tab: with a selection, every line it covers moves right one indent step (nothing is replaced);
    /// otherwise spaces up to the next step at the caret.
    /// </summary>
    public void IndentLines()
    {
        if (!HasSelection)
        {
            Insert(new string(' ', Indent - Column % Indent));
            return;
        }

        var (first, last) = SelectedLines();
        ShiftLines(first, last, line => _lines[line].Trim().Length == 0 ? 0 : Indent);
    }

    /// <summary>Shift+Tab: every line the selection covers (or the caret's) back one indent step, as far as its spaces go.</summary>
    public void OutdentLines()
    {
        var (first, last) = SelectedLines();
        ShiftLines(first, last, line =>
        {
            int spaces = _lines[line].Length - _lines[line].TrimStart(' ').Length;
            return -Math.Min(spaces, spaces % Indent == 0 ? Indent : spaces % Indent);
        });
    }

    /// <summary>
    /// Adds <paramref name="by"/> spaces at the start of each line (or takes them away when negative),
    /// keeping the caret and anchor on the same text.
    /// </summary>
    private void ShiftLines(int first, int last, Func<int, int> by)
    {
        bool changed = false;
        for (int line = first; line <= last; line++)
        {
            int shift = by(line);
            if (shift == 0)
                continue;
            changed = true;
            _lines[line] = shift > 0 ? new string(' ', shift) + _lines[line] : _lines[line][-shift..];
            if (Line == line)
                Column = Math.Max(0, Column + shift);
            if (Anchor is { } anchor && anchor.Line == line && (anchor.Column > 0 || shift < 0))
                Anchor = (line, Math.Max(0, anchor.Column + shift));
        }

        if (!changed)
            return;
        _wantedColumn = Column;
        Version++;
    }

    /// <summary>
    /// Ctrl+/: the lines the selection covers (or the caret's) become comments, each starting
    /// <paramref name="marker"/> and a space at their shared indent; when every line with text
    /// already is one, the markers come off instead. Blank lines are left alone.
    /// </summary>
    public void ToggleComment(string marker)
    {
        var (first, last) = SelectedLines();
        var lines = new List<int>();
        for (int line = first; line <= last; line++)
        {
            if (_lines[line].Trim().Length > 0)
                lines.Add(line);
        }

        if (lines.Count == 0)
            return;
        bool commented = lines.TrueForAll(line => _lines[line].TrimStart(' ').StartsWith(marker, StringComparison.Ordinal));
        int indent = int.MaxValue;
        foreach (int line in lines)
            indent = Math.Min(indent, _lines[line].Length - _lines[line].TrimStart(' ').Length);

        foreach (int line in lines)
        {
            string text = _lines[line];
            int at, length;
            if (commented)
            {
                at = text.Length - text.TrimStart(' ').Length;
                length = marker.Length + (text.Length > at + marker.Length && text[at + marker.Length] == ' ' ? 1 : 0);
                _lines[line] = text[..at] + text[(at + length)..];
                length = -length;
            }
            else
            {
                at = indent;
                length = marker.Length + 1;
                _lines[line] = text[..at] + marker + " " + text[at..];
            }

            // Text after the marker's place moves with it; a caret inside a removed marker goes to where it was.
            int Moved(int column) => column < at ? column : Math.Max(at, column + length);
            if (Line == line)
                Column = Moved(Column);
            if (Anchor is { } anchor && anchor.Line == line)
                Anchor = (line, Moved(anchor.Column));
        }

        _wantedColumn = Column;
        Version++;
    }

    /// <summary>
    /// Where a word move from the caret lands: past any spaces, then past a run of word characters
    /// or of symbols; at a line's end or start it crosses to the next or previous line.
    /// </summary>
    private (int Line, int Column) WordStop(int direction)
    {
        int line = Line, column = Column;
        string text = _lines[line];
        if (direction < 0)
        {
            if (column == 0)
                return line > 0 ? (line - 1, _lines[line - 1].Length) : (line, 0);
            while (column > 0 && text[column - 1] == ' ')
                column--;
            if (column > 0)
            {
                int kind = Kind(text[column - 1]);
                while (column > 0 && Kind(text[column - 1]) == kind && kind != 1)
                    column--;
            }
        }
        else
        {
            if (column == text.Length)
                return line + 1 < _lines.Count ? (line + 1, 0) : (line, column);
            while (column < text.Length && text[column] == ' ')
                column++;
            if (column < text.Length)
            {
                int kind = Kind(text[column]);
                while (column < text.Length && Kind(text[column]) == kind && kind != 1)
                    column++;
            }
        }

        return (line, column);
    }

    /// <summary>Ctrl+Left or Right: a word at a time. Without extending, a selection collapses to its side first.</summary>
    public void MoveWord(int direction, bool extend = false)
    {
        if (!extend && HasSelection)
        {
            MoveColumn(direction);
            return;
        }

        Extend(extend);
        (Line, Column) = WordStop(direction);
        _wantedColumn = Column;
    }

    /// <summary>Ctrl+Backspace or Delete: the word before or after the caret (and the spaces next to it), or the selection.</summary>
    public void DeleteWord(int direction)
    {
        if (DeleteSelection())
            return;
        (int line, int column) = WordStop(direction);
        if ((line, column) == (Line, Column))
            return;
        Anchor = (line, column);
        DeleteSelection();
    }

    /// <summary>Puts the caret at a line and column, clamped to the text; extending keeps the selection's anchor.</summary>
    public void Place(int line, int column, bool extend = false)
    {
        Extend(extend);
        Line = Math.Clamp(line, 0, _lines.Count - 1);
        Column = Math.Clamp(column, 0, _lines[Line].Length);
        _wantedColumn = Column;
    }

    /// <summary>Before a move: extending starts a selection at the caret if there is none, not extending drops it.</summary>
    private void Extend(bool extend)
    {
        if (!extend)
            Anchor = null;
        else
            Anchor ??= (Line, Column);
    }

    private static bool Before((int Line, int Column) a, (int Line, int Column) b) =>
        a.Line < b.Line || (a.Line == b.Line && a.Column < b.Column);

    /// <summary>0 for word characters, 1 for spaces, 2 for anything else (selected one at a time).</summary>
    private static int Kind(char c) => char.IsLetterOrDigit(c) || c == '_' ? 0 : c == ' ' ? 1 : 2;
}

/// <summary>A copy of the buffer for undo: the text, and where the caret and selection were.</summary>
public readonly record struct TextSnapshot(string Text, int Line, int Column, (int Line, int Column)? Anchor);

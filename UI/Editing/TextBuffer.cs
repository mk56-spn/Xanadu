using System;
using System.Collections.Generic;

namespace Xanadu.Ui.Editing;

/// <summary>
/// Text as lines, with one caret and the editing any text editor needs: typing,
/// new lines that keep the indentation,
/// Backspace that removes a whole indent step, Delete, and caret moves that
/// remember the column when passing short lines. A selection runs from an
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

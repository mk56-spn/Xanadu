using System;
using System.Collections.Generic;

namespace Xanadu.Labs.Editor;

/// <summary>
/// The text of the shader editor as lines, with one caret and the editing a
/// plain code editor needs: typing, new lines that keep the indentation,
/// Backspace that removes a whole indent step, Delete, and caret moves that
/// remember the column when passing short lines. No selection yet.
/// </summary>
internal sealed class CodeBuffer
{
    public const int Indent = 4;

    private readonly List<string> _lines = [""];

    /// <summary>The column up and down try to keep, so passing a short line doesn't lose it.</summary>
    private int _wantedColumn;

    public IReadOnlyList<string> Lines => _lines;

    public int Line { get; private set; }

    public int Column { get; private set; }

    /// <summary>Goes up on every change to the text (not on caret moves).</summary>
    public int Version { get; private set; }

    public string Text => string.Join('\n', _lines);

    public void Load(string text)
    {
        _lines.Clear();
        _lines.AddRange(text.ReplaceLineEndings("\n").Replace("\t", new string(' ', Indent)).Split('\n'));
        Line = 0;
        Column = 0;
        _wantedColumn = 0;
        Version++;
    }

    /// <summary>Types text at the caret; new lines in it split the line, tabs become spaces.</summary>
    public void Insert(string text)
    {
        if (text.Length == 0)
            return;

        string[] parts = text.ReplaceLineEndings("\n").Replace("\t", new string(' ', Indent)).Split('\n');
        string line = _lines[Line];
        string before = line[..Column], after = line[Column..];
        if (parts.Length == 1)
        {
            // A closing brace typed on a line of only spaces steps back one indent.
            if (parts[0] == "}" && before.Length >= Indent && before.Trim().Length == 0)
                before = before[..^Indent];
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

    /// <summary>Enter: splits the line, the new one indented like this one, one step more after an opening brace.</summary>
    public void NewLine()
    {
        string line = _lines[Line];
        int indent = line.Length - line.TrimStart(' ').Length;
        if (line[..Column].TrimEnd().EndsWith('{'))
            indent += Indent;
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

    /// <summary>Left or right by characters, crossing line ends.</summary>
    public void MoveColumn(int by)
    {
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
    public void MoveLine(int by)
    {
        Line = Math.Clamp(Line + by, 0, _lines.Count - 1);
        Column = Math.Min(_wantedColumn, _lines[Line].Length);
    }

    /// <summary>Home: to the first non-space character, or to column 0 if already there.</summary>
    public void Home()
    {
        string line = _lines[Line];
        int first = line.Length - line.TrimStart(' ').Length;
        Column = Column == first ? 0 : first;
        _wantedColumn = Column;
    }

    public void End()
    {
        Column = _lines[Line].Length;
        _wantedColumn = Column;
    }

    /// <summary>Puts the caret at a line and column, clamped to the text.</summary>
    public void Place(int line, int column)
    {
        Line = Math.Clamp(line, 0, _lines.Count - 1);
        Column = Math.Clamp(column, 0, _lines[Line].Length);
        _wantedColumn = Column;
    }
}

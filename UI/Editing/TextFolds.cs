using System.Collections.Generic;

namespace Xanadu.Ui.Editing;

/// <summary>
/// Which blocks of a text can fold and which are folded. A block is a pair of braces over several lines; it folds to
/// its header, the line holding the <c>{</c>, or the line above when the brace stands alone on its own (a method or a
/// struct written with the brace under it). Folded, every line after the header up to the closing brace is hidden.
/// Braces in line comments, <c>/* */</c> comments (where line comments are <c>//</c>) and quotes don't count.
/// </summary>
/// <remarks>
/// Kept for one buffer and brought up to date with <see cref="Update"/>: an edit above a fold moves it with its lines,
/// one that rewrites its header opens it, and one inside it leaves it folded.
/// </remarks>
public sealed class TextFolds
{
    /// <summary>Each header's last hidden line: the closing brace's.</summary>
    private readonly Dictionary<int, int> _ends = [];

    private readonly SortedSet<int> _folded = [];
    private string[] _seen = [];
    private int _version = -1;

    /// <summary>Goes up whenever a block folds or opens: what the drawing depends on besides the text.</summary>
    public int Version { get; private set; }

    /// <summary>The folded headers, top to bottom.</summary>
    public IReadOnlyCollection<int> Folded => _folded;

    /// <summary>Reads the blocks again when the text changed since the last call, moving the folds with their lines.</summary>
    public void Update(TextBuffer buffer, string? lineComment)
    {
        if (buffer.Version == _version)
            return;
        _version = buffer.Version;
        Shift(buffer.Lines);
        Read(buffer.Lines, lineComment);
        if (_folded.RemoveWhere(header => !_ends.ContainsKey(header)) > 0)
            Version++;
    }

    public bool CanFold(int line) => _ends.ContainsKey(line);

    public bool IsFolded(int line) => _folded.Contains(line);

    /// <summary>A header's last hidden line, the closing brace's.</summary>
    public int EndOf(int header) => _ends[header];

    /// <summary>The outermost folded header hiding <paramref name="line"/>, or -1 when it shows.</summary>
    public int Hiding(int line)
    {
        foreach (int header in _folded)
        {
            if (header >= line)
                break;
            if (_ends[header] >= line)
                return header;
        }

        return -1;
    }

    /// <summary>The innermost block <paramref name="line"/> is the header of or sits in, or -1 outside every block.</summary>
    public int BlockAt(int line)
    {
        if (_ends.ContainsKey(line))
            return line;
        int found = -1;
        foreach ((int header, int end) in _ends)
        {
            if (header < line && end >= line && header > found)
                found = header;
        }

        return found;
    }

    public void Fold(int header)
    {
        if (_ends.ContainsKey(header) && _folded.Add(header))
            Version++;
    }

    public void Open(int header)
    {
        if (_folded.Remove(header))
            Version++;
    }

    /// <summary>Opens every fold hiding <paramref name="line"/>.</summary>
    public void Show(int line)
    {
        if (_folded.RemoveWhere(header => header < line && _ends[header] >= line) > 0)
            Version++;
    }

    /// <summary>The next line shown after a shown one; past a folded header, the line after its block.</summary>
    public int Next(int line) => _folded.Contains(line) ? _ends[line] + 1 : line + 1;

    /// <summary>The line shown before a shown one: a folded block above it shows as its header.</summary>
    public int Previous(int line) => Hiding(line - 1) is var header and >= 0 ? header : line - 1;

    /// <summary><paramref name="by"/> shown lines on from <paramref name="line"/>, kept in a text of <paramref name="count"/> lines.</summary>
    public int Step(int line, int by, int count)
    {
        for (; by > 0 && Next(line) < count; by--)
            line = Next(line);
        for (; by < 0 && line > 0; by++)
            line = Previous(line);
        return line;
    }

    /// <summary>
    /// Moves the folds with their lines: the lines that stayed the same at the top and the bottom of the text pin the
    /// edit between them; a fold above it stays, one below moves by the lines added, one whose header it rewrote opens.
    /// </summary>
    private void Shift(IReadOnlyList<string> lines)
    {
        int before = _seen.Length, now = lines.Count, top = 0, bottom = 0;
        while (top < before && top < now && string.Equals(_seen[top], lines[top], System.StringComparison.Ordinal))
            top++;
        while (bottom < before - top && bottom < now - top
               && string.Equals(_seen[before - 1 - bottom], lines[now - 1 - bottom], System.StringComparison.Ordinal))
            bottom++;
        int added = now - before;
        if (_folded.Count > 0 && added != 0)
        {
            int[] folded = [.. _folded];
            _folded.Clear();
            foreach (int header in folded)
            {
                if (header < top)
                    _folded.Add(header);
                else if (header >= before - bottom)
                    _folded.Add(header + added);
            }

            Version++;
        }

        _seen = [.. lines];
    }

    /// <summary>Every block in the text: each brace's line paired with its closing one's, a header for each.</summary>
    private void Read(IReadOnlyList<string> lines, string? lineComment)
    {
        _ends.Clear();
        var open = new Stack<int>();
        bool blockComments = lineComment == "//", inComment = false;
        for (int index = 0; index < lines.Count; index++)
        {
            string line = lines[index];
            char quote = '\0';
            for (int at = 0; at < line.Length; at++)
            {
                char c = line[at];
                bool next(char wanted) => at + 1 < line.Length && line[at + 1] == wanted;
                if (inComment)
                {
                    if (c == '*' && next('/'))
                    {
                        inComment = false;
                        at++;
                    }
                }
                else if (quote != '\0')
                {
                    if (c == '\\')
                        at++;
                    else if (c == quote)
                        quote = '\0';
                }
                else if (lineComment is { Length: > 0 } && string.CompareOrdinal(line, at, lineComment, 0, lineComment.Length) == 0)
                    break;
                else if (blockComments && c == '/' && next('*'))
                {
                    inComment = true;
                    at++;
                }
                else if (c is '"' or '\'')
                    quote = c;
                else if (c == '{')
                    open.Push(index);
                else if (c == '}' && open.Count > 0 && open.Pop() is var opened && index > opened)
                    Add(lines, opened, index);
            }
        }
    }

    private void Add(IReadOnlyList<string> lines, int opened, int closed)
    {
        int header = opened;
        if (opened > 0 && lines[opened].TrimStart().StartsWith('{')
            && lines[opened - 1].Trim() is { Length: > 0 } above && !above.EndsWith(';') && !above.EndsWith('{') && !above.EndsWith('}'))
            header = opened - 1;
        if (!_ends.TryGetValue(header, out int end) || closed > end)
            _ends[header] = closed;
    }
}

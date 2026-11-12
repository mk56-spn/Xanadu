using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>One place a loop's index is named: where, how long, and how deep the loop is nested (0 outermost).</summary>
public readonly record struct LoopIndex(int Line, int Column, int Length, int Depth);

/// <summary>
/// Where each <c>for</c> loop's index is named, from its header to the end of
/// its body (a block, or the one statement after the header), so the editor can
/// colour it. A loop inside another is one deeper. Comments and strings don't count,
/// and neither does a field of the same name (<c>s.i</c>).
/// </summary>
public static partial class SlangLoops
{
    /// <summary>A for's header up to its index's name: <c>for (int i =</c>, <c>for (i =</c>.</summary>
    [GeneratedRegex(@"\bfor\s*\(\s*(?:(?:int|uint|var|let|float)\s+)?([A-Za-z_]\w*)\s*=")]
    private static partial Regex Header();

    public static List<LoopIndex> Find(IReadOnlyList<string> lines)
    {
        string text = string.Join('\n', lines);
        string code = Regex.Replace(text, @"/\*.*?\*/|//[^\n]*|""(?:[^""\\\n]|\\.)*""",
            m => Regex.Replace(m.Value, @"[^\n]", " "), RegexOptions.Singleline);

        var loops = new List<(int Start, int End, string Name)>();
        foreach (Match match in Header().Matches(code))
        {
            int open = code.IndexOf('(', match.Index);
            int close = Pair(code, open, '(', ')');
            int body = close;
            while (body < code.Length && char.IsWhiteSpace(code[body]))
                body++;
            int end = body < code.Length && code[body] == '{' ? Pair(code, body, '{', '}') : StatementEnd(code, body);
            loops.Add((match.Index, end, match.Groups[1].Value));
        }

        var found = new List<LoopIndex>();
        foreach (var loop in loops)
        {
            int depth = loops.FindAll(outer => outer.Start < loop.Start && loop.Start < outer.End).Count;
            foreach (Match use in Regex.Matches(code[loop.Start..loop.End], $@"(?<![\w.])\s*\b{Regex.Escape(loop.Name)}\b"))
            {
                int at = loop.Start + use.Index + (use.Length - loop.Name.Length);
                // Inside a loop nested in this one with the same name, the inner one's is meant.
                if (loops.Exists(inner => inner.Name == loop.Name && inner.Start > loop.Start && inner.Start <= at && at < inner.End))
                    continue;
                (int line, int column) = Place(code, at);
                found.Add(new LoopIndex(line, column, loop.Name.Length, depth));
            }
        }

        return found;
    }

    /// <summary>Just past the ; that ends the statement starting here (a nested for or if counts its own brackets).</summary>
    private static int StatementEnd(string code, int from)
    {
        int round = 0, curly = 0;
        for (int i = from; i < code.Length; i++)
        {
            switch (code[i])
            {
                case '(': round++; break;
                case ')': round--; break;
                case '{': curly++; break;
                case '}':
                    if (--curly == 0 && round == 0)
                        return i + 1;
                    break;
                case ';' when round == 0 && curly == 0:
                    return i + 1;
            }
        }

        return code.Length;
    }

    private static int Pair(string code, int open, char opener, char closer)
    {
        int depth = 0;
        for (int i = open; i < code.Length; i++)
        {
            if (code[i] == opener)
                depth++;
            else if (code[i] == closer && --depth == 0)
                return i + 1;
        }

        return code.Length;
    }

    private static (int Line, int Column) Place(string code, int index)
    {
        int line = 0, start = 0;
        for (int i = 0; i < index; i++)
        {
            if (code[i] == '\n')
            {
                line++;
                start = i + 1;
            }
        }

        return (line, index - start);
    }
}

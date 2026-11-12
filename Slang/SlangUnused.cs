using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>A name the editor shows faded, where it is declared, and why.</summary>
public readonly record struct Faded(int Line, int Column, int Length, string Reason);

/// <summary>
/// Names the code declares that do nothing, for the editor to fade:
/// <list type="bullet">
/// <item>never used after being declared (a uniform, a const, a function, a local, a field);</item>
/// <item>a field of the code's own struct that is read but never given a value, so it is always 0
/// (unless the struct is built whole somewhere: <c>State(…)</c>, an <c>__init</c>);</item>
/// <item>a local or a field given values that nothing reads.</item>
/// </list>
/// The places of a name are <see cref="SlangRename"/>'s. shade and update, which the host calls,
/// never fade, and neither do parameters for being written only (an <c>out</c> is meant to be).
/// </summary>
public static partial class SlangUnused
{
    public const string NeverUsed = "never used";
    public const string NeverGiven = "never given a value, so it is always 0";
    public const string NeverRead = "given values that nothing reads";

    /// <summary>What the host calls: used though the code never names them again.</summary>
    private static readonly HashSet<string> Entries = ["shade", "update"];

    private static readonly HashSet<string> NotTypes =
    [
        "return", "if", "else", "for", "while", "case", "break", "continue", "discard", "import", "module", "in", "out", "inout",
        "uniform", "static", "const", "public", "typedef", "using", "new", "do", "switch", "default", "let", "var",
    ];

    [GeneratedRegex(@"\b([A-Za-z_]\w*)(?:\s*<[^<>;{}]*>)?(?:\s*\[[^\]\n]*\])?\s+([A-Za-z_]\w*)\s*[=;,)(\[:]")]
    private static partial Regex Typed();

    [GeneratedRegex(@"\b(?:let|var)\s+([A-Za-z_]\w*)")]
    private static partial Regex Let();

    [GeneratedRegex(@"\bstruct\s+([A-Za-z_]\w*)[^{;]*\{")]
    private static partial Regex Struct();

    public static List<Faded> Find(IReadOnlyList<string> lines)
    {
        string text = string.Join('\n', lines);
        string code = Regex.Replace(text, @"/\*.*?\*/|//[^\n]*|""(?:[^""\\\n]|\\.)*""",
            m => Regex.Replace(m.Value, @"[^\n]", " "), RegexOptions.Singleline);
        int[] depth = Depths(code);
        var structs = new List<(string Name, int Open, int End)>();
        foreach (Match match in Struct().Matches(code))
        {
            int open = match.Index + match.Length - 1;
            if (depth[match.Index] == 0)
                structs.Add((match.Groups[1].Value, open, Close(code, open)));
        }

        var candidates = new List<(int At, string Name, bool Initialised)>();
        foreach (Match match in Typed().Matches(code))
        {
            if (!NotTypes.Contains(match.Groups[1].Value) && !NotTypes.Contains(match.Groups[2].Value) && !MemberAt(code, match.Index))
                candidates.Add((match.Groups[2].Index, match.Groups[2].Value, code[match.Index..(match.Index + match.Length)].TrimEnd().EndsWith('=')));
        }

        foreach (Match match in Let().Matches(code))
            candidates.Add((match.Groups[1].Index, match.Groups[1].Value, true));

        var found = new List<Faded>();
        var seen = new HashSet<int>();
        foreach ((int at, string name, bool initialised) in candidates.OrderBy(c => c.At))
        {
            if (!seen.Add(at))
                continue;
            var owner = structs.Find(s => s.Open < at && at < s.End);
            bool inStruct = owner.Name is not null;
            bool param = Parens(code, at);
            // Inside a struct only its fields count: its methods' parameters and locals are left alone.
            if (inStruct && (param || depth[at] != depth[owner.Open] + 1))
                continue;
            bool function = code.AsSpan(at + name.Length).TrimStart().StartsWith("(");
            if (function && depth[at] == 0 && Entries.Contains(name))
                continue;

            (int line, int column) = Place(code, at);
            if (SlangRename.Find(lines, line, column) is not { } rename)
                continue;
            var places = rename.Places.Select(p => Offset(lines, p.Line, p.Column)).ToList();
            foreach (int place in places)
                seen.Add(place);
            var others = places.Where(p => p != at).ToList();

            string? reason = null;
            if (others.Count == 0)
                reason = NeverUsed;
            else if (!function && (inStruct || (depth[at] > 0 && !param)))
            {
                bool read = others.Exists(p => Use(code, p, name.Length) is not Access.Write);
                bool written = initialised || others.Exists(p => Use(code, p, name.Length) is not Access.Read);
                if (!read)
                    reason = NeverRead;
                else if (!written && inStruct && !BuiltWhole(code, owner.Name, owner.Open, owner.End))
                    reason = NeverGiven;
            }

            if (reason is not null)
                found.Add(new Faded(line, column, name.Length, reason));
        }

        return found;
    }

    private enum Access { Read, Write, Both }

    /// <summary>
    /// How a place of a name uses it, looking past what follows it (<c>[i]</c>, <c>.x</c>): set by =,
    /// changed by += or ++ (read and written), handed to a method (<c>.paint(…)</c> may change it), or read.
    /// </summary>
    private static Access Use(string code, int at, int length)
    {
        int before = at - 1;
        while (before >= 0 && char.IsWhiteSpace(code[before]))
            before--;
        if (before >= 1 && code[before] == code[before - 1] && code[before] is '+' or '-')
            return Access.Both;

        int i = at + length;
        while (true)
        {
            while (i < code.Length && char.IsWhiteSpace(code[i]))
                i++;
            if (i < code.Length && code[i] == '[')
                i = Pair(code, i, '[', ']');
            else if (i < code.Length && code[i] == '.')
            {
                i++;
                while (i < code.Length && char.IsWhiteSpace(code[i]))
                    i++;
                while (i < code.Length && (char.IsLetterOrDigit(code[i]) || code[i] == '_'))
                    i++;
                int next = i;
                while (next < code.Length && char.IsWhiteSpace(code[next]))
                    next++;
                if (next < code.Length && code[next] == '(')
                    return Access.Both;
            }
            else
                break;
        }

        string rest = code[i..Math.Min(code.Length, i + 3)];
        if (rest.StartsWith("==", StringComparison.Ordinal))
            return Access.Read;
        if (rest.StartsWith('='))
            return Access.Write;
        if (rest.StartsWith("++", StringComparison.Ordinal) || rest.StartsWith("--", StringComparison.Ordinal)
            || Regex.IsMatch(rest, @"^(?:[+\-*/%&|^]=|<<=|>>=)"))
            return Access.Both;
        return Access.Read;
    }

    /// <summary>Whether the code makes a whole value of the struct (a constructor call, or an <c>__init</c> to call).</summary>
    private static bool BuiltWhole(string code, string name, int open, int end) =>
        code[open..end].Contains("__init", StringComparison.Ordinal) || Regex.IsMatch(code, $@"(?<![\w.]){Regex.Escape(name)}\s*\(");

    /// <summary>Whether an offset sits inside round brackets (a parameter list, a for's header).</summary>
    private static bool Parens(string code, int at)
    {
        int open = 0;
        for (int i = at - 1; i >= 0; i--)
        {
            if (code[i] is '{' or '}' or ';')
                return open > 0;
            open += code[i] == '(' ? 1 : code[i] == ')' ? -1 : 0;
        }

        return open > 0;
    }

    private static bool MemberAt(string code, int start)
    {
        int i = start - 1;
        while (i >= 0 && char.IsWhiteSpace(code[i]))
            i--;
        return i >= 0 && code[i] == '.';
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

    private static int Close(string code, int open) => Pair(code, open, '{', '}') - 1;

    private static int[] Depths(string code)
    {
        var depth = new int[code.Length + 1];
        int open = 0;
        for (int i = 0; i < code.Length; i++)
        {
            depth[i] = open;
            open = code[i] == '{' ? open + 1 : code[i] == '}' ? Math.Max(0, open - 1) : open;
        }

        depth[code.Length] = open;
        return depth;
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

    private static int Offset(IReadOnlyList<string> lines, int line, int column)
    {
        int offset = 0;
        for (int i = 0; i < line; i++)
            offset += lines[i].Length + 1;
        return offset + column;
    }
}

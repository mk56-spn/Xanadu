using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>
/// Where a name the code declares is used, for renaming it: every place of it
/// that means the same thing, comments and strings left out. A name declared
/// inside a function (a local or a parameter) is that function's; one declared
/// at the top (a uniform, a function, a struct) is the whole file's, except in
/// functions that declare their own. A field of the code's own struct is its
/// declaration and every <c>.field</c>. Names the code doesn't declare (the
/// library's, the host's, Slang's) can't be renamed.
/// </summary>
public static partial class SlangRename
{
    /// <summary>Words that can come before a name without declaring it (<c>return x;</c>).</summary>
    private static readonly HashSet<string> NotTypes =
    [
        "return", "if", "else", "for", "while", "case", "break", "continue", "discard", "import", "module", "in", "out", "inout",
        "uniform", "static", "const", "public", "typedef", "using", "new", "do", "switch", "default",
    ];

    [GeneratedRegex(@"/\*.*?\*/|//[^\n]*|""(?:[^""\\\n]|\\.)*""", RegexOptions.Singleline)]
    private static partial Regex CommentsAndStrings();

    /// <summary>
    /// Every place (line, column) of the name at a column, the one there included, or null when
    /// there is no name there or the code doesn't declare it.
    /// </summary>
    public static (string Name, List<(int Line, int Column)> Places)? Find(IReadOnlyList<string> lines, int line, int column)
    {
        string text = string.Join('\n', lines);
        // Comments and strings become spaces, so offsets stay the same and nothing in them matches.
        string code = CommentsAndStrings().Replace(text, m => Regex.Replace(m.Value, @"[^\n]", " "));
        int at = Offset(lines, line, column);
        if (at < 0 || at > code.Length)
            return null;

        static bool Word(char c) => char.IsLetterOrDigit(c) || c == '_';
        int start = at, end = at;
        while (start > 0 && Word(code[start - 1]))
            start--;
        while (end < code.Length && Word(code[end]))
            end++;
        if (start == end || char.IsDigit(code[start]))
            return null;
        string name = code[start..end];

        List<Block> blocks = TopBlocks(code);
        var uses = Regex.Matches(code, $@"\b{name}\b").Select(m => (Index: m.Index, Member: MemberAt(code, m.Index))).ToList();
        List<int> declarations = Declarations(code, name);
        // The block each declaration belongs to: null for the top (a uniform, or a function's or struct's own name).
        Block? Owner(int d) => BlockOf(blocks, d) is { } b && (d > b.Open || Parens(code, b.Start, d) > 0) ? b : null;
        bool member = MemberAt(code, start) || declarations.Exists(d => Owner(d) is { Struct: true } block && block.Start <= start && start < block.End);

        List<int> found;
        if (member)
        {
            // A field: declared in one of the code's structs, used after a dot.
            if (!declarations.Exists(d => Owner(d) is { Struct: true }))
                return null;
            found = uses.Where(u => u.Member || declarations.Contains(u.Index) && Owner(u.Index) is { Struct: true })
                .Select(u => u.Index).ToList();
        }
        else
        {
            var plain = uses.Where(u => !u.Member).Select(u => u.Index).ToList();
            // Declared in the function round the caret: only that function's.
            if (BlockOf(blocks, start) is { Struct: false } here && declarations.Exists(d => Owner(d) == here))
                found = plain.Where(u => BlockOf(blocks, u) == here).ToList();
            else if (declarations.Exists(d => Owner(d) is null))
            {
                // Declared at the top: everywhere but functions that declare their own.
                var shadowing = blocks.Where(b => !b.Struct && declarations.Exists(d => Owner(d) == b)).ToList();
                found = plain.Where(u => BlockOf(blocks, u) is not { } b || !shadowing.Contains(b)).ToList();
            }
            else
                return null;
        }

        if (!found.Contains(start))
            return null;
        return (name, found.Select(index => Place(code, index)).ToList());
    }

    /// <summary>A top-level block: from <see cref="Start"/> (just after the ; or } before it) through its { at <see cref="Open"/> to <see cref="End"/>.</summary>
    private readonly record struct Block(int Start, int Open, int End, bool Struct);

    /// <summary>How many round brackets are open at an offset, counting from a block's start (its parameters).</summary>
    private static int Parens(string code, int from, int to)
    {
        int depth = 0;
        for (int i = from; i < to; i++)
            depth += code[i] == '(' ? 1 : code[i] == ')' ? -1 : 0;
        return depth;
    }

    /// <summary>Whether a name at an offset follows a dot (a member).</summary>
    private static bool MemberAt(string code, int start)
    {
        int i = start - 1;
        while (i >= 0 && code[i] is ' ' or '\t' or '\n')
            i--;
        return i >= 0 && code[i] == '.';
    }

    /// <summary>
    /// Where the name is declared: after a type (<c>float x</c>, <c>State s</c>, <c>float4 shade(</c>,
    /// <c>float2[8] points</c>), after let or var, or as a struct's name.
    /// </summary>
    private static List<int> Declarations(string code, string name)
    {
        var found = new List<int>();
        foreach (Match match in Regex.Matches(code, $@"\b([A-Za-z_]\w*)(\s*<[^<>;{{}}]*>)?(\s*\[[^\]\n]*\])?\s+({name})\s*[=;,)(\[:]"))
        {
            if (!NotTypes.Contains(match.Groups[1].Value) && !MemberAt(code, match.Index))
                found.Add(match.Groups[4].Index);
        }

        foreach (Match match in Regex.Matches(code, $@"\b(?:let|var|struct)\s+({name})\b"))
        {
            if (!found.Contains(match.Groups[1].Index))
                found.Add(match.Groups[1].Index);
        }

        return found;
    }

    /// <summary>The top-level blocks, each with what comes before its { (so a function's parameters are its own).</summary>
    private static List<Block> TopBlocks(string code)
    {
        var blocks = new List<Block>();
        int depth = 0, region = 0, open = -1;
        for (int i = 0; i < code.Length; i++)
        {
            char c = code[i];
            if (c == '{')
            {
                if (depth++ == 0)
                    open = i;
            }
            else if (c == '}' && depth > 0 && --depth == 0)
            {
                bool isStruct = Regex.IsMatch(code[region..open], @"\bstruct\b");
                blocks.Add(new Block(region, open, i + 1, isStruct));
                region = i + 1;
            }
            else if (c == ';' && depth == 0)
                region = i + 1;
        }

        if (depth > 0 && open >= 0)
            blocks.Add(new Block(region, open, code.Length, Regex.IsMatch(code[region..open], @"\bstruct\b")));
        return blocks;
    }

    private static Block? BlockOf(List<Block> blocks, int index)
    {
        foreach (var block in blocks)
        {
            if (block.Start <= index && index < block.End)
                return block;
        }

        return null;
    }

    private static int Offset(IReadOnlyList<string> lines, int line, int column)
    {
        if (line < 0 || line >= lines.Count)
            return -1;
        int offset = 0;
        for (int i = 0; i < line; i++)
            offset += lines[i].Length + 1;
        return offset + Math.Clamp(column, 0, lines[line].Length);
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

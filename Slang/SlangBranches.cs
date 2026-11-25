using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>A switch's body (from its '{' to its '}') and the stretch of text each of its cases runs, in order.</summary>
public readonly record struct SwitchCases(int Start, int End, List<(int Start, int End)> Cases);

/// <summary>
/// Which parts of the code never both run for one pixel: the cases of a <c>switch</c> (each
/// from just after its label, <c>case …:</c> or <c>default:</c>, to the next label or the end of
/// the body; a nested switch's labels are its own), and a block that ends in <c>return</c>
/// against everything after it. Read from the text; expects code with comments stripped.
/// </summary>
public sealed class SlangBranches
{
    private static readonly Regex If = new(@"\b(?:if\s*\(|else\b)", RegexOptions.Compiled);

    /// <summary>A block, braced or one statement, whose last statement is a return.</summary>
    private static readonly Regex EndsInReturn = new(@"\breturn\b[^;]*;\s*\}?\s*$", RegexOptions.Compiled);

    private readonly List<(int Start, int End)> _returning = [];

    /// <summary>Every switch, outermost and nested, in text order.</summary>
    public List<SwitchCases> Switches { get; }

    public SlangBranches(string code)
    {
        Switches = Find(code);
        foreach (Match branch in If.Matches(code))
        {
            int start = branch.Index + branch.Length;
            if (branch.Value.StartsWith("if", StringComparison.Ordinal))
                start = Closing(code, start - 1, '(', ')') + 1;
            while (start < code.Length && char.IsWhiteSpace(code[start]))
                start++;
            // else if: the if after it is a branch of its own.
            if (start >= code.Length || code.AsSpan(start).StartsWith("if"))
                continue;
            int end = code[start] == '{' ? Closing(code, start, '{', '}') + 1 : code.IndexOf(';', start) + 1;
            if (end > start && EndsInReturn.IsMatch(code[start..end]))
                _returning.Add((start, end));
        }
    }

    /// <summary>
    /// Whether two places never both run: different cases of one switch, or one inside a block
    /// that returns and the other after that block.
    /// </summary>
    public bool Apart(int a, int b)
    {
        if (a > b)
            (a, b) = (b, a);
        foreach ((int start, int end) in _returning)
            if (start <= a && a < end && b >= end)
                return true;
        foreach (SwitchCases s in Switches)
        {
            int caseA = s.Cases.FindIndex(c => c.Start <= a && a < c.End);
            int caseB = s.Cases.FindIndex(c => c.Start <= b && b < c.End);
            if (caseA >= 0 && caseB >= 0 && caseA != caseB)
                return true;
        }

        return false;
    }

    private static readonly Regex Header = new(@"\bswitch\s*\(", RegexOptions.Compiled);

    /// <summary>A case label; <c>::</c> inside it is a scope, not its colon.</summary>
    private static readonly Regex Label = new(@"\b(?:case\b(?:[^:;{}]|::)*|default\s*):(?!:)", RegexOptions.Compiled);

    private static List<SwitchCases> Find(string code)
    {
        var switches = new List<SwitchCases>();
        foreach (Match header in Header.Matches(code))
        {
            int close = Closing(code, header.Index + header.Length - 1, '(', ')');
            int open = close + 1;
            while (open < code.Length && char.IsWhiteSpace(code[open]))
                open++;
            if (open >= code.Length || code[open] != '{')
                continue;
            int end = Closing(code, open, '{', '}');

            var labels = new List<Match>();
            foreach (Match label in Label.Matches(code[..end], open + 1))
                if (Depth(code, open + 1, label.Index) == 0)
                    labels.Add(label);

            var cases = new List<(int, int)>();
            for (int i = 0; i < labels.Count; i++)
                cases.Add((labels[i].Index + labels[i].Length, i + 1 < labels.Count ? labels[i + 1].Index : end));
            switches.Add(new SwitchCases(open, end, cases));
        }

        return switches;
    }

    /// <summary>
    /// The most one run through <c>[from, to)</c> costs, each place's cost at where it sits: a
    /// switch costs its dearest case, and a block that returns costs the dearer of itself and
    /// all that follows it (the two never both run).
    /// </summary>
    public float Dearest(int from, int to, IReadOnlyList<(int At, float Cost)> costs)
    {
        float Plain(int start, int end) => costs.Where(c => start <= c.At && c.At < end).Sum(c => c.Cost);
        bool Within((int Start, int End) span) => from <= span.Start && span.End <= to && (span.Start, span.End) != (from, to);

        // The first switch or returning block in the range: what comes before it runs plainly.
        SwitchCases? first = Switches.Where(s => Within((s.Start, s.End))).OrderBy(s => s.Start).Cast<SwitchCases?>().FirstOrDefault();
        (int Start, int End)? returns = _returning.Where(Within).OrderBy(r => r.Start).Cast<(int, int)?>().FirstOrDefault();
        if (first is { } s && (returns is not { } r0 || s.Start <= r0.Start))
            return Plain(from, s.Start) + s.Cases.Select(c => Dearest(c.Start, c.End, costs)).DefaultIfEmpty(0).Max() + Dearest(s.End, to, costs);
        if (returns is { } r)
            return Plain(from, r.Start) + Math.Max(Dearest(r.Start, r.End, costs), Dearest(r.End, to, costs));
        return Plain(from, to);
    }

    /// <summary>The bracket closing the one at <paramref name="open"/>, or the end of the code.</summary>
    private static int Closing(string code, int open, char opens, char closes)
    {
        int depth = 0;
        for (int i = open; i < code.Length; i++)
        {
            if (code[i] == opens)
                depth++;
            else if (code[i] == closes && --depth == 0)
                return i;
        }

        return code.Length;
    }

    /// <summary>How many braces are open at <paramref name="at"/>, counting from <paramref name="from"/>.</summary>
    private static int Depth(string code, int from, int at)
    {
        int depth = 0;
        for (int i = from; i < at; i++)
            depth += code[i] == '{' ? 1 : code[i] == '}' ? -1 : 0;
        return depth;
    }
}

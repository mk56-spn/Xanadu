using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Xanadu.Labs.Editor;

/// <summary>One complaint from Slang: where (1-based; line 0 when it isn't in the author's code) and what.</summary>
internal readonly record struct SlangProblem(int Line, int Column, string Message, bool Warning);

/// <summary>
/// Reads Slang's diagnostics for code compiled under a <c>#line 1 "label"</c>
/// (what <c>SlangShaders.Wrap</c> does), in both forms it prints:
/// <code>
/// error[E30015]: undefined identifier
///  --> label:4:20
///   |
/// 4 | let c = Circle(radius: 20.0);
///   |                ^^^^^^ undefined identifier 'radius'.
/// </code>
/// and the older one-line <c>label(6): error 10005: newline in literal</c>.
/// </summary>
internal static partial class SlangProblems
{
    [GeneratedRegex(@"^(error|warning)\[[^\]]*\]:\s*(.*)$")]
    private static partial Regex Header();

    [GeneratedRegex(@"^\s*-->\s*(.+):(\d+):(\d+)\s*$")]
    private static partial Regex Location();

    [GeneratedRegex(@"^\s*\|\s*\^+\s*(.*)$")]
    private static partial Regex Caret();

    [GeneratedRegex(@"^(.+)\((\d+)\):\s*(error|warning)\s*\d*:\s*(.*)$")]
    private static partial Regex OneLine();

    public static List<SlangProblem> Parse(string output, string label)
    {
        var problems = new List<SlangProblem>();
        string[] lines = output.ReplaceLineEndings("\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            Match oneLine = OneLine().Match(lines[i]);
            if (oneLine.Success)
            {
                bool ours = oneLine.Groups[1].Value.Trim() == label;
                problems.Add(new SlangProblem(
                    ours ? int.Parse(oneLine.Groups[2].Value) : 0, 1,
                    oneLine.Groups[4].Value.Trim(), oneLine.Groups[3].Value == "warning"));
                continue;
            }

            Match header = Header().Match(lines[i]);
            if (!header.Success)
                continue;

            string message = header.Groups[2].Value.Trim();
            int line = 0, column = 1;
            // The location and the caret's own wording follow within a few lines.
            for (int j = i + 1; j < Math.Min(lines.Length, i + 8) && !Header().IsMatch(lines[j]); j++)
            {
                Match location = Location().Match(lines[j]);
                if (location.Success && location.Groups[1].Value.Trim() == label)
                {
                    line = int.Parse(location.Groups[2].Value);
                    column = int.Parse(location.Groups[3].Value);
                }

                Match caret = Caret().Match(lines[j]);
                if (caret.Success && caret.Groups[1].Value.Trim().Length > 0)
                    message = caret.Groups[1].Value.Trim();
            }

            problems.Add(new SlangProblem(line, column, message, header.Groups[1].Value == "warning"));
        }

        // Slang reports some mistakes twice in the two forms; keep the first of each.
        var seen = new HashSet<(int, string)>();
        problems.RemoveAll(problem => !seen.Add((problem.Line, problem.Message)));
        if (problems.Count == 0 && output.Trim().Length > 0)
            problems.Add(new SlangProblem(0, 1, output.Trim().Split('\n')[0], false));
        return problems;
    }
}

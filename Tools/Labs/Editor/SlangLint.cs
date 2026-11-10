using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Xanadu.Labs.Editor;

/// <summary>
/// The editor's own warnings, for mistakes Slang doesn't report on code that
/// compiles. Each is a rule in <c>docs/shader-style.md</c>, named in its message.
/// <list type="bullet">
/// <item>XS003: a local declared without a value (<c>colour c;</c>) whose first
/// use reads it. Slang leaves locals as whatever was there, so the result is
/// noise. A first use that assigns it, or one of its fields (<c>c = …</c>,
/// <c>t.id = …</c>), is fine.</item>
/// </list>
/// Read as text, a statement at a time: no flow analysis, so a value set in
/// only one branch of an <c>if</c> before it is read isn't caught.
/// </summary>
internal static partial class SlangLint
{
    [GeneratedRegex(@"\b([A-Za-z_]\w*)\s+([A-Za-z_]\w*(?:\s*,\s*[A-Za-z_]\w*)*)\s*;")]
    private static partial Regex Declaration();

    [GeneratedRegex(@"^\s*(?:(?:\.\s*\w+)|(?:\[[^\]]*\]))*\s*=(?!=)")]
    private static partial Regex AssignedAfter();

    /// <param name="typeNames">Types a local can be declared with: Slang's and the library's.</param>
    public static List<SlangProblem> Check(string source, IReadOnlySet<string> typeNames)
    {
        string code = Blank(source);
        var problems = new List<SlangProblem>();
        int[] depth = Depths(code);
        foreach (Match match in Declaration().Matches(code))
        {
            // Inside a function only: file-level uniforms and statics are set by the host.
            if (depth[match.Index] == 0 || !typeNames.Contains(match.Groups[1].Value))
                continue;
            int end = ScopeEnd(code, match.Index + match.Length);
            foreach ((int at, string name) in Names(match))
            {
                Match use = Regex.Match(code[(match.Index + match.Length)..end], $@"(?<![\w.]){Regex.Escape(name)}\b");
                if (!use.Success)
                    continue;
                int after = match.Index + match.Length + use.Index + use.Length;
                if (AssignedAfter().IsMatch(code[after..Math.Min(code.Length, after + 200)]))
                    continue;

                (int line, int column) = Place(code, at);
                problems.Add(new SlangProblem(
                    line, column,
                    $"XS003 · {name} is used before it has a value; give it one here ({match.Groups[1].Value} {name} = …)",
                    Warning: true));
            }
        }

        return problems;
    }

    /// <summary>Each declared name, with its place in the source.</summary>
    private static IEnumerable<(int At, string Name)> Names(Match match)
    {
        Group names = match.Groups[2];
        foreach (Match name in Regex.Matches(names.Value, @"[A-Za-z_]\w*"))
            yield return (names.Index + name.Index, name.Value);
    }

    /// <summary>The source with comments and strings blanked to spaces, so places don't move.</summary>
    private static string Blank(string source)
    {
        string Spaces(Match m) => Regex.Replace(m.Value, @"[^\n]", " ");
        return Regex.Replace(source, @"/\*.*?\*/|//[^\n]*|""(?:[^""\\\n]|\\.)*""", Spaces, RegexOptions.Singleline);
    }

    /// <summary>How many braces are open at each character.</summary>
    private static int[] Depths(string code)
    {
        var depth = new int[code.Length + 1];
        int open = 0;
        for (int i = 0; i < code.Length; i++)
        {
            depth[i] = open;
            if (code[i] == '{')
                open++;
            else if (code[i] == '}')
                open = Math.Max(0, open - 1);
        }

        depth[code.Length] = open;
        return depth;
    }

    /// <summary>Where the block a declaration sits in closes.</summary>
    private static int ScopeEnd(string code, int from)
    {
        int open = 0;
        for (int i = from; i < code.Length; i++)
        {
            if (code[i] == '{')
                open++;
            else if (code[i] == '}' && --open < 0)
                return i;
        }

        return code.Length;
    }

    private static (int Line, int Column) Place(string code, int index)
    {
        int line = 1, start = 0;
        for (int i = 0; i < index; i++)
        {
            if (code[i] == '\n')
            {
                line++;
                start = i + 1;
            }
        }

        return (line, index - start + 1);
    }
}

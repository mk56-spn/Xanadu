using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>
/// The editor's own warnings, for mistakes Slang doesn't report on code that
/// compiles. Each is a rule in <c>Docs/shader-style.md</c>, named in its message.
/// <list type="bullet">
/// <item>XS002: a division of two whole numbers (<c>1 / trail</c> with
/// <c>int trail</c>) where a float is wanted: the fraction is dropped before
/// it becomes one, so <c>1 / 30</c> is 0. Not where an int is wanted: an index
/// (<c>[n / 2]</c>), a for's header, an int being given a value, an int returned.</item>
/// <item>XS003: a local declared without a value (<c>colour c;</c>) whose first
/// use reads it. Slang leaves locals as whatever was there, so the result is
/// noise. A first use that assigns it, or one of its fields (<c>c = …</c>,
/// <c>t.id = …</c>), is fine, and so is passing it alone to an <c>out</c> parameter
/// of a function the code declares (<c>map(uv, near, neighbor)</c>), which fills it.</item>
/// <item>XS004: <c>mouse</c> passed to <c>anchor</c>/<c>anchored</c>. Anchors are
/// fractions of the quad; <c>mouse</c> is pixels, so the shape lands far off the
/// stage. <c>move(mouse)</c> is what was meant.</item>
/// <item>XS005, XS006: shape work, in SlangLint.Cost.cs.</item>
/// <item>XS007: a parameter or local named like a function it's in the way of
/// (<c>float length</c>, then <c>length(d)</c> in its scope). A value can't be called,
/// so Slang says "no call operation found for type" at the call; the warning goes on
/// the name, where the fix is.</item>
/// <item>XS008: a <c>deform</c> that bends (a mesh surface, <c>place</c>, <c>wave</c>,
/// <c>twist</c>, <c>bulge</c>, <c>sway</c>, <c>curl</c>, <c>jitter</c>, <c>mesh.split</c>)
/// with no <c>[Grid(columns, rows)]</c> on it. The shader tool's preview draws its
/// own grid, so it looks right there, but on a level item it has four corners and
/// the bend comes out flat.</item>
/// </list>
/// Read as text, a statement at a time: no flow analysis, so a value set in
/// only one branch of an <c>if</c> before it is read isn't caught.
/// </summary>
public static partial class SlangLint
{
    [GeneratedRegex(@"\b([A-Za-z_]\w*)\s+([A-Za-z_]\w*(?:\s*,\s*[A-Za-z_]\w*)*)\s*;")]
    private static partial Regex Declaration();

    [GeneratedRegex(@"^\s*(?:(?:\.\s*\w+)|(?:\[[^\]]*\]))*\s*=(?!=)")]
    private static partial Regex AssignedAfter();

    [GeneratedRegex(@"\.\s*anchor(?:ed)?\s*\(")]
    private static partial Regex AnchorCall();

    [GeneratedRegex(@"(?<![\w.])mouse\b")]
    private static partial Regex Mouse();

    /// <param name="typeNames">Types a local can be declared with: Slang's and the library's.</param>
    /// <param name="shapeCosts">Each shape's cost from the library's tags (<see cref="SlangHints.Costs"/>); without it the cost rules don't run.</param>
    public static List<SlangProblem> Check(string source, IReadOnlySet<string> typeNames, IReadOnlyDictionary<string, float>? shapeCosts = null)
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
                if (AssignedAfter().IsMatch(code[after..Math.Min(code.Length, after + 200)])
                    || OutArgument(code, match.Index + match.Length + use.Index, after))
                    continue;

                (int line, int column) = Place(code, at);
                problems.Add(new SlangProblem(
                    line, column,
                    $"XS003 · {name} is used before it has a value; give it one here ({match.Groups[1].Value} {name} = …)",
                    Warning: true));
            }
        }

        problems.AddRange(MouseAnchors(code));
        problems.AddRange(WholeDivisions(code));
        problems.AddRange(HiddenFunctions(code, typeNames));
        problems.AddRange(GridlessBends(code));
        if (shapeCosts is { Count: > 0 })
            problems.AddRange(ShapeCosts(code, shapeCosts));
        problems.Sort((a, b) => a.Line != b.Line ? a.Line.CompareTo(b.Line) : a.Column.CompareTo(b.Column));
        return problems;
    }

    /// <summary>A name followed by an optional generic list and a '(': where a function's header might start.</summary>
    [GeneratedRegex(@"\b([A-Za-z_]\w*)\s*(?:<[^<>(){};]*>)?\s*\(")]
    private static partial Regex NameThenParen();

    /// <summary>A local given a type and a name: <c>float l =</c>, <c>let d =</c>, <c>float2 a, b;</c>.</summary>
    [GeneratedRegex(@"\b([A-Za-z_]\w*)\s+([A-Za-z_]\w*)\s*(?==(?!=)|;|,)")]
    private static partial Regex LocalName();

    private static readonly HashSet<string> NotFunctions = ["if", "for", "while", "switch", "return", "else"];

    /// <summary>Each function the code declares: its parameter list (inside the brackets) and its body (inside the braces).</summary>
    private static List<(int ParamsFrom, int ParamsTo, int BodyFrom, int BodyTo)> Functions(string code)
    {
        var functions = new List<(int, int, int, int)>();
        foreach (Match header in NameThenParen().Matches(code))
        {
            if (NotFunctions.Contains(header.Groups[1].Value) || !Regex.IsMatch(code[..header.Index], @"[\w>]\s+$"))
                continue;
            int open = header.Index + header.Length - 1, depth = 0, close = open;
            for (; close < code.Length; close++)
            {
                if (code[close] == '(')
                    depth++;
                else if (code[close] == ')' && --depth == 0)
                    break;
            }

            int brace = close + 1;
            while (brace < code.Length && char.IsWhiteSpace(code[brace]))
                brace++;
            if (brace < code.Length && code[brace] == '{')
                functions.Add((open + 1, close, brace + 1, ScopeEnd(code, brace + 1)));
        }

        return functions;
    }

    /// <summary>XS007: each parameter or local whose name is called as a function where it's in scope.</summary>
    private static IEnumerable<SlangProblem> HiddenFunctions(string code, IReadOnlySet<string> typeNames)
    {
        var functions = Functions(code);
        var names = new List<(int At, string Name, int From, int To)>();
        foreach ((int paramsFrom, int paramsTo, int bodyFrom, int bodyTo) in functions)
        {
            string list = code[paramsFrom..paramsTo];
            int depth = 0, start = 0;
            for (int i = 0; i <= list.Length; i++)
            {
                if (i < list.Length && list[i] is '(' or '<' or '[')
                    depth++;
                else if (i < list.Length && list[i] is ')' or '>' or ']')
                    depth--;
                else if (i == list.Length || (list[i] == ',' && depth == 0))
                {
                    string head = list[start..i].Split('=')[0];
                    Match name = Regex.Match(head, @"([A-Za-z_]\w*)\s*$");
                    if (name.Success && !typeNames.Contains(name.Groups[1].Value))
                        names.Add((paramsFrom + start + name.Groups[1].Index, name.Groups[1].Value, bodyFrom, bodyTo));
                    start = i + 1;
                }
            }
        }

        foreach (Match local in LocalName().Matches(code))
        {
            string type = local.Groups[1].Value;
            if (!(typeNames.Contains(type) || type is "let" or "var") || !functions.Exists(f => f.BodyFrom <= local.Index && local.Index < f.BodyTo))
                continue;
            int from = local.Index + local.Length;
            names.Add((local.Groups[2].Index, local.Groups[2].Value, from, ScopeEnd(code, from)));
        }

        foreach ((int at, string name, int from, int to) in names)
        {
            foreach (Match call in Regex.Matches(code[from..to], $@"(?<![\w.]){Regex.Escape(name)}\s*\("))
            {
                // A declaration of a function by that name (a type before it) isn't a call.
                Match before = Regex.Match(code[..(from + call.Index)], @"([\w>]+)\s+$");
                if (before.Success && (typeNames.Contains(before.Groups[1].Value) || before.Groups[1].Value.EndsWith('>')))
                    continue;
                (int line, int column) = Place(code, at);
                int callLine = Place(code, from + call.Index).Line;
                yield return new SlangProblem(line, column,
                    $"XS007 · {name} here hides the {name}() function, so the call on line {callLine} can't reach it; give this another name",
                    Warning: true);
                break;
            }
        }
    }

    /// <summary>A name declared int or uint (<c>int count</c>, <c>static const int trail</c>, <c>for (int i …</c>).</summary>
    [GeneratedRegex(@"\b(?:int|uint)\s+([A-Za-z_]\w*)")]
    private static partial Regex IntName();

    /// <summary>A function returning int or uint, up to its body's brace.</summary>
    [GeneratedRegex(@"\b(?:int|uint)\s+[A-Za-z_]\w*\s*\([^)]*\)\s*\{")]
    private static partial Regex IntFunction();

    /// <summary>A whole-number literal (<c>2</c>, <c>2u</c>), not part of a float (<c>2.5</c>, <c>.5</c>, <c>2e3</c>).</summary>
    private static bool WholeLiteral(string word) => Regex.IsMatch(word, @"^\d+[uU]?$");

    /// <summary>XS002: each whole-number division where a float is wanted.</summary>
    private static IEnumerable<SlangProblem> WholeDivisions(string code)
    {
        var ints = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in IntName().Matches(code))
            ints.Add(match.Groups[1].Value);
        var intBodies = new List<(int Start, int End)>();
        foreach (Match match in IntFunction().Matches(code))
            intBodies.Add((match.Index + match.Length, ScopeEnd(code, match.Index + match.Length)));

        bool Whole(string word) => WholeLiteral(word) || ints.Contains(word);

        for (int at = 0; at < code.Length; at++)
        {
            if (code[at] != '/' || (at + 1 < code.Length && code[at + 1] is '=' or '/') || (at > 0 && code[at - 1] == '/'))
                continue;

            // The operand just before: a word not after a dot (a.b / 2 is a field, unknown here).
            int leftEnd = at;
            while (leftEnd > 0 && code[leftEnd - 1] is ' ' or '\t')
                leftEnd--;
            int leftStart = leftEnd;
            while (leftStart > 0 && (char.IsLetterOrDigit(code[leftStart - 1]) || code[leftStart - 1] == '_'))
                leftStart--;
            if (leftStart == leftEnd || (leftStart > 0 && code[leftStart - 1] == '.'))
                continue;

            // The operand just after: a word not followed by a call, a field or an index.
            int rightStart = at + 1;
            while (rightStart < code.Length && code[rightStart] is ' ' or '\t')
                rightStart++;
            int rightEnd = rightStart;
            while (rightEnd < code.Length && (char.IsLetterOrDigit(code[rightEnd]) || code[rightEnd] == '_'))
                rightEnd++;
            if (rightStart == rightEnd || (rightEnd < code.Length && code[rightEnd] is '.' or '(' or '['))
                continue;

            string left = code[leftStart..leftEnd], right = code[rightStart..rightEnd];
            if (!Whole(left) || !Whole(right) || !WholeProduct(code, leftStart, Whole) || WantsInt(code, leftStart, ints, intBodies))
                continue;

            (int line, int column) = Place(code, leftStart);
            string fix = WholeLiteral(left) ? $"{left.TrimEnd('u', 'U')}.0 / {right}" : $"float({left}) / {right}";
            yield return new SlangProblem(
                line, column,
                $"XS002 · {left} / {right} divides whole numbers, so the fraction is dropped; write {fix}",
                Warning: true);
        }
    }

    /// <summary>
    /// Whether the factors before a division's left word (<c>2.0 * extent * k / n</c>: the
    /// <c>/</c> divides the whole product) are all whole numbers too; one that isn't a plain
    /// whole word (a float, a call, a bracket) makes the product unknown, so it's let be.
    /// </summary>
    private static bool WholeProduct(string code, int leftStart, Func<string, bool> whole)
    {
        int at = leftStart;
        while (true)
        {
            int op = at;
            while (op > 0 && code[op - 1] is ' ' or '\t')
                op--;
            if (op == 0 || code[op - 1] is not ('*' or '/' or '%') || (op > 1 && code[op - 2] == '/'))
                return true;
            int end = op - 1;
            while (end > 0 && code[end - 1] is ' ' or '\t')
                end--;
            int start = end;
            while (start > 0 && (char.IsLetterOrDigit(code[start - 1]) || code[start - 1] is '_' or '.'))
                start--;
            if (start == end || !whole(code[start..end]))
                return false;
            at = start;
        }
    }

    /// <summary>
    /// Whether the expression at an offset gives an int: inside an index's brackets or a for's
    /// header, the value of an int being declared or assigned, or what an int function returns.
    /// </summary>
    private static bool WantsInt(string code, int at, HashSet<string> ints, List<(int Start, int End)> intBodies)
    {
        int square = 0, round = 0;
        int i = at - 1;
        for (; i >= 0 && code[i] is not (';' or '{' or '}'); i--)
        {
            if (code[i] == ']')
                square--;
            else if (code[i] == '[' && ++square > 0)
                return true;
            else if (code[i] == ')')
                round--;
            else if (code[i] == '(' && ++round > 0 && Regex.IsMatch(code[..i], @"\bfor\s*$"))
                return true;
        }

        // A for's header has ;s of its own: its condition and step sit after one.
        if (i >= 0 && code[i] == ';' && Regex.IsMatch(code[..(i + 1)], @"\bfor\s*\([^)]*$"))
            return true;

        string statement = code[(i + 1)..at].TrimStart();
        Match target = Regex.Match(statement, @"^(?:(?:static|const)\s+)*(?:(int|uint)\s+\w+|(\w+)(?:\s*\[[^\]]*\])?)\s*[+\-*%]?=(?!=)");
        if (target.Success && (target.Groups[1].Success || ints.Contains(target.Groups[2].Value)))
            return true;
        return statement.StartsWith("return", StringComparison.Ordinal) && intBodies.Exists(b => b.Start <= at && at < b.End);
    }

    [GeneratedRegex(@"\bItemVertex\s+(deform)\s*\(")]
    private static partial Regex DeformHeader();

    [GeneratedRegex(@"\[\s*Grid\s*\(")]
    private static partial Regex GridAttribute();

    /// <summary>Calls that need corners inside the quad: surfaces placed on it, and the moves that bend rather than squash, tilt or shift.</summary>
    [GeneratedRegex(@"\.\s*(?:deformed|place|placed|wave|waved|twist|twisted|bulge|bulged|sway|swayed|curl|curled|jitter|jittered)\s*\(|\bmesh\s*\.\s*split\s*\(")]
    private static partial Regex Bend();

    /// <summary>XS008: a deform that bends, with no [Grid] to give it corners to bend on a level item.</summary>
    private static IEnumerable<SlangProblem> GridlessBends(string code)
    {
        Match header = DeformHeader().Match(code);
        if (!header.Success || GridAttribute().IsMatch(code) || !Bend().IsMatch(code))
            yield break;

        (int line, int column) = Place(code, header.Groups[1].Index);
        yield return new SlangProblem(
            line, column,
            "XS008 · this deform bends, but a level item has only four corners; put [Grid(96, 54)] on it (columns, rows) so it has corners inside",
            Warning: true);
    }

    /// <summary>XS004: each <c>anchor(…)</c> or <c>anchored(…)</c> whose argument uses <c>mouse</c>.</summary>
    private static IEnumerable<SlangProblem> MouseAnchors(string code)
    {
        foreach (Match call in AnchorCall().Matches(code))
        {
            int start = call.Index + call.Length, open = 1, i = start;
            for (; i < code.Length && open > 0; i++)
            {
                if (code[i] == '(')
                    open++;
                else if (code[i] == ')')
                    open--;
            }

            Match mouse = Mouse().Match(code[start..Math.Max(start, i - 1)]);
            if (!mouse.Success)
                continue;

            string method = code.Substring(call.Index, call.Length).Contains("anchored") ? "moved" : "move";
            (int line, int column) = Place(code, start + mouse.Index);
            yield return new SlangProblem(
                line, column,
                $"XS004 · anchor takes a fraction of the quad, but mouse is in pixels; use {method}(mouse)",
                Warning: true);
        }
    }

    /// <summary>Each declared name, with its place in the source.</summary>
    /// <summary>
    /// Whether the name between <paramref name="start"/> and <paramref name="end"/> is a whole argument
    /// of a call to a function the code declares, at a parameter declared <c>out</c>.
    /// </summary>
    private static bool OutArgument(string code, int start, int end)
    {
        int depth = 0, index = 0, open = -1;
        for (int i = start - 1; i >= 0 && open < 0; i--)
        {
            char c = code[i];
            if (c is ')' or ']' or '}')
                depth++;
            else if (c is '(' or '[' or '{' && depth > 0)
                depth--;
            else if (c == '(')
                open = i;
            else if (c is '{' or ';')
                return false;
            else if (c == ',' && depth == 0)
                index++;
        }

        // The argument must be the name alone: nothing but space between it and the commas or brackets round it.
        int before = start - 1;
        while (before > open && char.IsWhiteSpace(code[before]))
            before--;
        int next = end;
        while (next < code.Length && char.IsWhiteSpace(code[next]))
            next++;
        if (open < 0 || (code[before] != ',' && before != open) || next >= code.Length || code[next] is not (',' or ')'))
            return false;

        Match callee = Regex.Match(code[..open], @"([A-Za-z_]\w*)\s*$");
        if (!callee.Success)
            return false;
        Match declared = Regex.Match(code, $@"\b[A-Za-z_]\w*\s+{Regex.Escape(callee.Groups[1].Value)}\s*\(([^)]*)\)\s*\{{");
        if (!declared.Success)
            return false;
        string[] parameters = declared.Groups[1].Value.Split(',');
        return index < parameters.Length && Regex.IsMatch(parameters[index], @"^\s*out\b");
    }

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

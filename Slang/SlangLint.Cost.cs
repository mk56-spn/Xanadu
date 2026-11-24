using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>
/// The shape-cost warnings, read from the text like the other rules. A shape is
/// a recipe: making, copying or joining one costs nothing, but every draw of it
/// (<c>filled</c>, <c>stroked</c>, <c>paint(shape, …)</c>, <c>measured</c>, …)
/// works its distance out again, every shape inside it included. Each shape's
/// cost is its library <c>/// @cost</c> tag, a Circle being 1.
/// <list type="bullet">
/// <item>XS005: one shape worked out more than once at the same pixel, unchanged in
/// between: <c>.measured()</c> works it out once and every draw after reuses it.
/// A move, turn, scale or anchor in between makes it another shape, so that isn't caught.</item>
/// <item>XS006: the whole effect's shape work past <see cref="ShapeBudget"/> a pixel,
/// loops counted by their bound (a bound the text doesn't give is taken as
/// <see cref="UnknownPasses"/> passes).</item>
/// </list>
/// Only what the text says: a helper function counts once wherever it's called from,
/// an if counts as though it always runs, and a switch costs its dearest case.
/// </summary>
public static partial class SlangLint
{
    /// <summary>The most shape work a pixel should do, in Circles, before XS006 warns.</summary>
    public const float ShapeBudget = 32;

    /// <summary>The passes a loop is taken to make when its bound isn't a number the text gives.</summary>
    public const int UnknownPasses = 8;

    /// <summary>Calls that work a shape's distance out at the pixel.</summary>
    private static readonly HashSet<string> Draws = ["filled", "stroked", "outlined", "feathered", "haloed", "shadowed", "contoured", "shown", "distance", "measured"];

    /// <summary>Calls on a colour that draw the shape given first.</summary>
    private static readonly HashSet<string> Paints = ["paint", "painted", "erase", "erased"];

    /// <summary>
    /// Joins: with a Measured on either side they work out the shape on the other, there and then
    /// (<c>m.minus(shape)</c> and <c>shape.minus(m)</c> alike).
    /// </summary>
    private static readonly HashSet<string> Joins = ["and", "or", "minus", "melted", "meltedAnd", "meltedMinus"];

    /// <summary>Calls that change a shape where it is, making it another shape from then on.</summary>
    private static readonly HashSet<string> Changes = ["move", "rotate", "scale", "anchor"];

    /// <summary>What a value is made of: its cost when drawn, the shape names (at their versions) inside it, and whether it's already measured.</summary>
    private sealed record Recipe(float Cost, HashSet<string> Shapes, bool Measured);

    /// <summary>One place a shape is worked out: where, what it costs, which named shapes it works out, and at what point ("" for the pixel).</summary>
    private readonly record struct Work(int At, float Cost, HashSet<string> Shapes, string Point);

    [GeneratedRegex(@"(?<![\w.\]])([A-Za-z_]\w*)\s*=(?!=)")]
    private static partial Regex Assignment();

    [GeneratedRegex(@"(?<![\w.])([A-Za-z_]\w*)\s*\.\s*(move|rotate|scale|anchor)\s*\(")]
    private static partial Regex Change();

    [GeneratedRegex(@"\.\s*([A-Za-z_]\w*)\s*\(")]
    private static partial Regex MethodCall();

    [GeneratedRegex(@"(\.\s*)?([A-Za-z_]\w*)(\s*\()?")]
    private static partial Regex Token();

    [GeneratedRegex(@"\bfor\s*\(")]
    private static partial Regex ForHeader();

    private static IEnumerable<SlangProblem> ShapeCosts(string code, IReadOnlyDictionary<string, float> costs)
    {
        // Every change of a name: an assignment or a change in place. A shape's version is how many came before.
        var changed = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        void Changed(string name, int at)
        {
            if (!changed.TryGetValue(name, out var list))
                changed[name] = list = [];
            list.Add(at);
        }

        string Version(string name, int at) => name + "@" + (changed.TryGetValue(name, out var list) ? list.Count(c => c < at) : 0);

        // The shapes the code keeps in names, each with where it was given its value.
        var named = new Dictionary<string, List<(int At, Recipe Recipe)>>(StringComparer.Ordinal);
        Recipe? Named(string name, int at) =>
            named.TryGetValue(name, out var list) ? list.LastOrDefault(v => v.At < at).Recipe : null;

        Recipe? Read(string value, int at)
        {
            float cost = 0;
            var shapes = new HashSet<string>(StringComparer.Ordinal);
            bool measured = false, any = false;
            // sdf.melted and the rest work on numbers, not shapes.
            value = Regex.Replace(value, @"\bsdf\s*\.\s*\w+", "sdf_");
            string?[] within = Within(value);
            foreach (Match token in Token().Matches(value))
            {
                string name = token.Groups[2].Value;
                bool dotted = token.Groups[1].Success, called = token.Groups[3].Success;
                // In moved(...), at(...), a number's maths: a shape there gives a point or a number, it isn't drawn.
                if (!dotted && !called && within[token.Index] is { } call && !Joins.Contains(call))
                    continue;
                // s.points[k], s.count: reading a shape's field or an element isn't drawing it.
                if (!dotted && !called && Regex.IsMatch(value[(token.Index + token.Length)..], @"^\s*(?:\.\s*[A-Za-z_]\w*(?!\s*[\w(])|\[)"))
                    continue;
                if (dotted && called && Draws.Contains(name) && name != "measured")
                    return null;     // a number now, not a shape
                if (dotted && called && name == "measured")
                {
                    // Worked out by the measured call itself; what follows joins a Measured.
                    (cost, measured, any) = (0, true, true);
                    shapes.Clear();
                    continue;
                }

                if (measured)
                    continue;
                if (called && costs.TryGetValue(name, out float own))
                {
                    cost += own;
                    any = true;
                }
                else if (!dotted && !called && Named(name, at) is { } shape)
                {
                    any = true;
                    if (shape.Measured)
                        (cost, measured) = (0, true);
                    else
                    {
                        cost += shape.Cost;
                        shapes.UnionWith(shape.Shapes);
                        shapes.Add(Version(name, at));
                    }
                }
            }

            return any ? new Recipe(cost, shapes, measured) : null;
        }

        foreach (Match change in Change().Matches(code))
            Changed(change.Groups[1].Value, change.Index);
        foreach (Match assignment in Assignment().Matches(code))
        {
            string name = assignment.Groups[1].Value;
            int from = assignment.Index + assignment.Length;
            string value = code[from..StatementEnd(code, from)];
            Changed(name, assignment.Index);
            // A name declared as a number, a colour, a Step… is never a shape, whatever it's made from.
            string? type = DeclaredType(code, name, assignment.Index);
            if (type is not null && !IsShapeType(type, costs))
                continue;
            // A shape made by code the lint can't read (a helper) still costs what its type does.
            Recipe? read = Read(value, assignment.Index);
            if (read is null && type is not null && costs.TryGetValue(type, out float own))
                read = new Recipe(own, [], false);
            if (read is { } recipe)
            {
                if (!named.TryGetValue(name, out var list))
                    named[name] = list = [];
                list.Add((assignment.Index, recipe));
            }
        }

        // Every place a shape is worked out.
        var work = new List<Work>();
        foreach (Match call in MethodCall().Matches(code))
        {
            string method = call.Groups[1].Value;
            int open = call.Index + call.Length - 1;
            Recipe? done = null;
            float times = 1;
            string point = "";
            if (Draws.Contains(method))
            {
                done = Read(Receiver(code, call.Index), call.Index);
                point = Point(code, method, Arguments(code, open));
            }
            else if (Paints.Contains(method))
            {
                List<string> args = Arguments(code, open);
                done = args.Count > 0 ? Read(args[0], call.Index) : null;
                // A Look paints the fill, then the outline: twice.
                if (args.Count > 1 && IsLook(code, args[1].Trim()))
                    times = 2;
            }
            else if (Joins.Contains(method))
            {
                List<string> args = Arguments(code, open);
                Recipe? receiver = Read(Receiver(code, call.Index), call.Index);
                Recipe? other = args.Count > 0 ? Read(args[0], call.Index) : null;
                if (receiver is { Measured: true })
                    done = other;
                else if (other is { Measured: true })
                    done = receiver;
            }

            if (done is { Measured: false, Cost: > 0 })
                work.Add(new Work(call.Index, done.Cost * times, done.Shapes, point));
        }

        // XS005: a named shape worked out again, unchanged, at the same point. Draws that never
        // both run (different cases of a switch, a block that returns and what follows) aren't repeats.
        var branches = new SlangBranches(code);
        var seen = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        var told = new HashSet<string>(StringComparer.Ordinal);
        foreach (Work done in work.OrderBy(w => w.At))
        {
            foreach (string shape in done.Shapes)
            {
                string key = shape + "|" + done.Point;
                if (!seen.TryGetValue(key, out var before))
                    seen[key] = before = [];
                bool again = before.Any(at => !branches.Apart(at, done.At));
                before.Add(done.At);
                if (!again || !told.Add(shape))
                    continue;
                string name = shape[..shape.IndexOf('@')];
                (int line, int column) = Place(code, done.At);
                yield return new SlangProblem(line, column,
                    $"XS005 · {name} is worked out again here; measure it once (let m = {name}.measured()) and draw that",
                    Warning: true);
            }
        }

        // XS006: the whole effect over the budget.
        List<(int Start, int End, float Passes, bool Guessed)> loops = Loops(code);
        bool guessed = false;
        var weighed = new List<(int At, float Cost)>();
        foreach (Work done in work)
        {
            float passes = 1;
            foreach (var loop in loops.Where(l => l.Start <= done.At && done.At < l.End))
            {
                passes *= loop.Passes;
                guessed |= loop.Guessed;
            }

            weighed.Add((done.At, done.Cost * passes));
        }

        // Only one case of a switch runs at a pixel, and a block that returns ends it: the dearest way through.
        float total = branches.Dearest(0, code.Length, weighed);

        if (total > ShapeBudget)
        {
            Match shade = Regex.Match(code, @"\bshade\s*\(");
            (int line, int column) = Place(code, shade.Success ? shade.Index : 0);
            string loopsNote = guessed ? $", a loop without a fixed count taken as {UnknownPasses} passes" : "";
            yield return new SlangProblem(line, column,
                $"XS006 · about {total:0} circles of shape work a pixel, over the {ShapeBudget:0} budget{loopsNote}; measure shapes drawn more than once, or draw fewer",
                Warning: true);
        }
    }

    /// <summary>For each character, the call whose arguments it is in (innermost; "" for plain brackets), or null at the top.</summary>
    private static string?[] Within(string value)
    {
        var within = new string?[value.Length + 1];
        var open = new Stack<string>();
        for (int i = 0; i < value.Length; i++)
        {
            within[i] = open.Count > 0 ? open.Peek() : null;
            if (value[i] == '(')
            {
                int end = i;
                while (end > 0 && char.IsWhiteSpace(value[end - 1]))
                    end--;
                int start = end;
                while (start > 0 && (char.IsLetterOrDigit(value[start - 1]) || value[start - 1] == '_'))
                    start--;
                open.Push(value[start..end]);
            }
            else if (value[i] == ')' && open.Count > 0)
                open.Pop();
        }

        within[value.Length] = open.Count > 0 ? open.Peek() : null;
        return within;
    }

    /// <summary>The type a name was last declared with before <paramref name="at"/> (<c>float scale =</c>, <c>Spline s;</c>); null when the text doesn't say.</summary>
    private static string? DeclaredType(string code, string name, int at)
    {
        string? type = null;
        foreach (Match declared in Regex.Matches(code, $@"\b([A-Za-z_]\w*)\s+{Regex.Escape(name)}\b\s*(?=[=;,)])"))
        {
            if (declared.Index > at)
                break;
            if (declared.Groups[1].Value is not ("return" or "in" or "out" or "inout" or "const" or "static" or "uniform"))
                type = declared.Groups[1].Value;
        }

        return type;
    }

    /// <summary>Whether values of a type can be shapes: a library shape (one with a cost), Shape, Measured, or inferred (let, var).</summary>
    private static bool IsShapeType(string type, IReadOnlyDictionary<string, float> costs) =>
        type is "let" or "var" or "Shape" or "Measured" || costs.ContainsKey(type);

    /// <summary>
    /// The point a draw works its shape out at: its first argument when that's a point, "" for the
    /// pixel (none given, or canvas.centred). distance and measured take only a point; the others
    /// take a point first only when it's there, so a number or a name declared as one isn't.
    /// </summary>
    private static string Point(string code, string method, List<string> args)
    {
        if (args.Count == 0)
            return "";
        string first = Regex.Replace(args[0], @"\s+", "");
        bool number = Regex.IsMatch(first, @"^[-+]?(\d+\.?\d*|\.\d+)([eE][-+]?\d+)?[fF]?$")
                      || Regex.IsMatch(code, $@"\b(?:float|int|uint|half)\s+{Regex.Escape(first)}\b");
        if (method is not ("distance" or "measured") && number)
            return "";
        return first is "canvas.centred" ? "" : first;
    }

    /// <summary>Whether an argument is a Look: made as one, or a name declared as one.</summary>
    private static bool IsLook(string code, string argument) =>
        Regex.IsMatch(argument, @"^Look\s*\(") || Regex.IsMatch(code, $@"\bLook\s+{Regex.Escape(argument)}\b");

    /// <summary>The expression a call is made on: the chain of names, calls and indexes before the dot at <paramref name="dot"/>.</summary>
    private static string Receiver(string code, int dot)
    {
        int i = dot;
        while (true)
        {
            int j = i;
            while (j > 0 && char.IsWhiteSpace(code[j - 1]))
                j--;
            if (j > 0 && code[j - 1] is ')' or ']')
            {
                // Back over the brackets, then the name they follow.
                int depth = 0;
                for (j--; j >= 0; j--)
                {
                    if (code[j] is ')' or ']')
                        depth++;
                    else if (code[j] is '(' or '[' && --depth == 0)
                        break;
                }

                if (j < 0)
                    break;
            }

            while (j > 0 && (char.IsLetterOrDigit(code[j - 1]) || code[j - 1] == '_'))
                j--;
            if (j == i)
                break;
            i = j;
            int k = i;
            while (k > 0 && char.IsWhiteSpace(code[k - 1]))
                k--;
            if (k == 0 || code[k - 1] != '.')
                break;
            i = k - 1;
        }

        return code[i..dot];
    }

    /// <summary>The top-level arguments of the call whose '(' is at <paramref name="open"/>.</summary>
    private static List<string> Arguments(string code, int open)
    {
        var args = new List<string>();
        int depth = 0, start = open + 1;
        for (int i = open; i < code.Length; i++)
        {
            if (code[i] is '(' or '[' or '{')
                depth++;
            else if (code[i] is ')' or ']' or '}' && --depth == 0)
            {
                args.Add(code[start..i]);
                break;
            }
            else if (code[i] == ',' && depth == 1)
            {
                args.Add(code[start..i]);
                start = i + 1;
            }
        }

        return [.. args.Where(a => a.Trim().Length > 0)];
    }

    /// <summary>Where the statement a value starts in ends: its ';' or the bracket closing round it.</summary>
    private static int StatementEnd(string code, int from)
    {
        int depth = 0;
        for (int i = from; i < code.Length; i++)
        {
            if (code[i] is '(' or '[' or '{')
                depth++;
            else if (code[i] is ')' or ']' or '}' && --depth < 0)
                return i;
            else if (code[i] is ';' or ',' && depth == 0)
                return i;
        }

        return code.Length;
    }

    /// <summary>Each for loop's body and its passes: its bound when that's a number or a constant the code gives, else a guess.</summary>
    private static List<(int Start, int End, float Passes, bool Guessed)> Loops(string code)
    {
        var loops = new List<(int, int, float, bool)>();
        foreach (Match header in ForHeader().Matches(code))
        {
            int open = header.Index + header.Length - 1, depth = 0, close = open;
            for (; close < code.Length; close++)
            {
                if (code[close] == '(')
                    depth++;
                else if (code[close] == ')' && --depth == 0)
                    break;
            }

            if (close >= code.Length)
                continue;
            string head = code[(open + 1)..close];
            Match bound = Regex.Match(head, @"<\s*(=)?\s*([A-Za-z_]\w*|\d+)\s*;");
            float passes = UnknownPasses;
            bool guessed = true;
            if (bound.Success)
            {
                string limit = bound.Groups[2].Value;
                Match constant = Regex.Match(code, $@"\bconst\s+u?int\s+{Regex.Escape(limit)}\s*=\s*(\d+)");
                string? number = char.IsDigit(limit[0]) ? limit : constant.Success ? constant.Groups[1].Value : null;
                if (number is not null)
                {
                    passes = int.Parse(number) + (bound.Groups[1].Success ? 1 : 0);
                    guessed = false;
                }
            }

            int start = close + 1;
            while (start < code.Length && char.IsWhiteSpace(code[start]))
                start++;
            int end = start < code.Length && code[start] == '{' ? ScopeEnd(code, start + 1) : StatementEnd(code, start);
            loops.Add((start, end, passes, guessed));
        }

        return loops;
    }
}

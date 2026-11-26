using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>
/// One argument as written: where it is in the code (offsets into the text with its lines
/// joined by <c>'\n'</c>), its text, and its value when the CPU can work it out. Only a plain
/// literal (<c>60</c>, <c>float2(40, -12)</c>) or a bare tuned value's name can be written back.
/// </summary>
public sealed record ChainArgument(int Start, int End, string Text, float[]? Value, bool Literal, string? Uniform)
{
    public bool Writable => Value is not null && (Literal || Uniform is not null);
}

/// <summary>One <c>.method(…)</c> after a shape: its name, its brackets' close and arguments, and the handle its tag gives (moved: Move).</summary>
public sealed record ChainStep(string Name, int Close, IReadOnlyList<ChainArgument> Arguments, HandleKind? Kind);

/// <summary>
/// A shape made in the code and what is done to it: <c>Circle(60).moved(float2(40, 0)).rotated(0.3)</c>.
/// A shape given to another's step (<c>a.melted(Box(…), 20)</c>) is a chain of its own whose
/// <see cref="Parent"/> is that chain at that step; one inside another's constructor sits in its
/// space (<see cref="ParentStep"/> -1).
/// </summary>
public sealed class ShapeChain(string shape, int start, int close, IReadOnlyList<ChainArgument> arguments)
{
    public string Shape { get; } = shape;

    /// <summary>Where the shape's name starts.</summary>
    public int Start { get; } = start;

    /// <summary>Where its constructor's closing bracket is.</summary>
    public int Close { get; } = close;

    public IReadOnlyList<ChainArgument> Arguments { get; } = arguments;
    public List<ChainStep> Steps { get; } = [];

    /// <summary>Just after its last step's closing bracket.</summary>
    public int End { get; internal set; }

    public ShapeChain? Parent { get; internal set; }
    public int ParentStep { get; internal set; }

    public bool Holds(int offset) => offset >= Start && offset <= End;
}

/// <summary>
/// Finds the shapes in a shader's code (<see cref="ShapeChain"/>), a struct being a shape when
/// the library says so (<see cref="SlangHandleTags.IsShape"/>). Comments are skipped; a chain may
/// run over lines. A value is worked out from literals, the tuned values and the operators the
/// CPU knows (<see cref="BuildExpression"/>); anything else is left unknown.
/// </summary>
public static partial class SlangShapeChains
{
    [GeneratedRegex(@"^[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?[fF]?$")]
    private static partial Regex Number();

    [GeneratedRegex(@"^float2\s*\((.*)\)$", RegexOptions.Singleline)]
    private static partial Regex Float2();

    [GeneratedRegex(@"^[A-Za-z_]\w*$")]
    private static partial Regex Name();

    /// <summary>Every shape chain in <paramref name="text"/>, outer ones before the ones in their arguments.</summary>
    /// <param name="values">A tuned value or constant by name (one number or two); null for a name it doesn't know.</param>
    public static List<ShapeChain> Find(string text, SlangHandleTags tags, Func<string, float[]?> values)
    {
        string code = Masked(text);
        var found = new List<ShapeChain>();
        Scan(code, 0, code.Length, tags, values, null, 0, found);
        return found;
    }

    /// <summary>The text with every comment turned to spaces, so offsets stay the same.</summary>
    public static string Masked(string text)
    {
        char[] chars = text.ToCharArray();
        for (int i = 0; i < chars.Length - 1; i++)
        {
            if (chars[i] == '/' && chars[i + 1] == '/')
            {
                for (; i < chars.Length && chars[i] != '\n'; i++)
                    chars[i] = ' ';
            }
            else if (chars[i] == '/' && chars[i + 1] == '*')
            {
                int end = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                end = end < 0 ? chars.Length : end + 2;
                for (; i < end; i++)
                    chars[i] = chars[i] == '\n' ? '\n' : ' ';
                i--;
            }
        }

        return new string(chars);
    }

    private static void Scan(string code, int from, int to, SlangHandleTags tags, Func<string, float[]?> values,
        ShapeChain? parent, int parentStep, List<ShapeChain> found)
    {
        int i = from;
        while (i < to)
        {
            if (!IsNameStart(code[i]) || (i > 0 && IsNamePart(code[i - 1])))
            {
                i++;
                continue;
            }

            int nameEnd = i;
            while (nameEnd < to && IsNamePart(code[nameEnd]))
                nameEnd++;
            string name = code[i..nameEnd];
            int open = SkipSpace(code, nameEnd, to);
            bool method = Before(code, i) == '.';
            if (!method && open < to && code[open] == '(' && tags.IsShape(name) && Matching(code, open, to) is int close)
            {
                ShapeChain chain = Read(code, name, i, open, close, to, tags, values);
                chain.Parent = parent;
                chain.ParentStep = parentStep;
                found.Add(chain);
                // What it is given: shapes in its constructor sit in its space, ones in a step after it.
                Scan(code, open + 1, close, tags, values, chain, -1, found);
                int at = close + 1;
                for (int step = 0; step < chain.Steps.Count; step++)
                {
                    int stepOpen = code.IndexOf('(', at);
                    Scan(code, stepOpen + 1, chain.Steps[step].Close, tags, values, chain, step, found);
                    at = chain.Steps[step].Close + 1;
                }

                i = chain.End;
                continue;
            }

            i = nameEnd;
        }
    }

    private static ShapeChain Read(string code, string name, int start, int open, int close, int to, SlangHandleTags tags, Func<string, float[]?> values)
    {
        var chain = new ShapeChain(name, start, close, Arguments(code, open, close, values));
        int at = close + 1;
        while (true)
        {
            int dot = SkipSpace(code, at, to);
            if (dot >= to || code[dot] != '.')
                break;
            int nameStart = SkipSpace(code, dot + 1, to), nameEnd = nameStart;
            while (nameEnd < to && IsNamePart(code[nameEnd]))
                nameEnd++;
            int stepOpen = SkipSpace(code, nameEnd, to);
            if (nameEnd == nameStart || stepOpen >= to || code[stepOpen] != '(' || Matching(code, stepOpen, to) is not int stepClose)
                break;
            string method = code[nameStart..nameEnd];
            IReadOnlyList<HandleTag> stepTags = tags.Of(method);
            HandleKind? kind = stepTags.Count > 0 && stepTags[0].Kind is HandleKind.Move or HandleKind.Turn or HandleKind.Scale ? stepTags[0].Kind : null;
            chain.Steps.Add(new ChainStep(method, stepClose, Arguments(code, stepOpen, stepClose, values), kind));
            at = stepClose + 1;
        }

        chain.End = at;
        return chain;
    }

    /// <summary>The arguments between a call's brackets, split at its own commas.</summary>
    private static List<ChainArgument> Arguments(string code, int open, int close, Func<string, float[]?> values)
    {
        var arguments = new List<ChainArgument>();
        int depth = 0, start = open + 1;
        for (int i = open + 1; i <= close; i++)
        {
            char c = code[i];
            if (c is '(' or '[' or '{')
                depth++;
            else if (c is ')' or ']' or '}' && i < close)
                depth--;
            else if ((c == ',' && depth == 0) || i == close)
            {
                int s = SkipSpace(code, start, i), e = i;
                while (e > s && char.IsWhiteSpace(code[e - 1]))
                    e--;
                if (e > s)
                    arguments.Add(Argument(code[s..e], s, e, values));
                start = i + 1;
            }
        }

        return arguments;
    }

    private static ChainArgument Argument(string text, int start, int end, Func<string, float[]?> values)
    {
        if (Number().IsMatch(text))
            return new ChainArgument(start, end, text, [Parse(text)], true, null);
        if (Float2().Match(text) is { Success: true } vector)
        {
            string[] parts = vector.Groups[1].Value.Split(',');
            bool literal = Array.TrueForAll(parts, part => Number().IsMatch(part.Trim()));
            float[]? parsed = parts.Length switch
            {
                1 when Scalar(parts[0], values) is float both => [both, both],
                2 when Scalar(parts[0], values) is float x && Scalar(parts[1], values) is float y => [x, y],
                _ => null,
            };
            return new ChainArgument(start, end, text, parsed, literal && parsed is not null, null);
        }

        if (Name().IsMatch(text) && values(text) is { } named)
            return new ChainArgument(start, end, text, named, false, text);
        float? worked = Scalar(text, values);
        return new ChainArgument(start, end, text, worked is float value ? [value] : null, false, null);
    }

    private static float? Scalar(string text, Func<string, float[]?> values)
    {
        try
        {
            BuildValue value = BuildExpression.Parse(text).Evaluate(name => values(name) is [float one] ? new BuildValue(one, false) : null);
            return (float)value.Number;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>The closing bracket that matches the one at <paramref name="open"/>, or null when it doesn't close before <paramref name="to"/>.</summary>
    private static int? Matching(string code, int open, int to)
    {
        int depth = 0;
        for (int i = open; i < to; i++)
        {
            if (code[i] is '(' or '[')
                depth++;
            else if (code[i] is ')' or ']' && --depth == 0)
                return i;
        }

        return null;
    }

    private static char Before(string code, int at)
    {
        for (int i = at - 1; i >= 0; i--)
        {
            if (!char.IsWhiteSpace(code[i]))
                return code[i];
        }

        return '\0';
    }

    private static int SkipSpace(string code, int at, int to)
    {
        while (at < to && char.IsWhiteSpace(code[at]))
            at++;
        return at;
    }

    private static bool IsNameStart(char c) => char.IsAsciiLetter(c) || c == '_';
    private static bool IsNamePart(char c) => char.IsAsciiLetterOrDigit(c) || c == '_';

    private static float Parse(string number) =>
        float.Parse(number.TrimEnd('f', 'F'), NumberStyles.Float, CultureInfo.InvariantCulture);
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>A plain <c>float2(x, y)</c> in the code that is a point: where it is in the text, its value, and why it's a point.</summary>
/// <param name="Line">Its line, from 0.</param>
/// <param name="Start">The column its <c>float2</c> starts at.</param>
/// <param name="End">The column just after its closing bracket.</param>
/// <param name="Value">The two numbers.</param>
/// <param name="Name">What it fills: the library parameter (<c>offset</c>) or the variable it's given to.</param>
public readonly record struct PointLiteral(int Line, int Start, int End, Vector2 Value, string Name)
{
    /// <summary>Whether a caret at <paramref name="column"/> on <paramref name="line"/> is on it, either end included.</summary>
    public bool Holds(int line, int column) => line == Line && column >= Start && column <= End;
}

/// <summary>
/// The code's points the editor can drag on the preview: <c>float2(x, y)</c> with two plain
/// numbers, given to a library parameter that is a place in pixels from the quad's centre
/// (<see cref="Places"/>: <c>moved(offset)</c>, <c>Line(a, b)</c>, <c>angleTo(target)</c>), or
/// to a variable whose name isn't a size or the like (<see cref="IsPlaceName"/>). Anything
/// worked out (<c>float2(i * 20, 0)</c>) is left alone, and so is a size (<c>Box(float2(80, 10))</c>).
/// Only what one line says: a call split over lines isn't followed.
/// </summary>
public static partial class SlangPoints
{
    /// <summary>The library's parameter names that are a place in pixels from the quad's centre.</summary>
    public static readonly IReadOnlySet<string> Places = new HashSet<string>(StringComparer.Ordinal)
    {
        "position", "offset", "target", "focus", "centre", "start", "end", "control1", "control2", "a", "b",
    };

    /// <summary>Words in a name that make it something other than a place: a size, a direction, a range.</summary>
    private static readonly string[] NotPlaces =
    [
        "size", "spacing", "radius", "scale", "uv", "range", "seed", "speed", "velocity", "dir", "facing",
        "count", "freq", "amount", "strength", "stretch", "squash", "tile", "cell", "resolution",
    ];

    private const string Number = @"([-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?)[fF]?";

    [GeneratedRegex(@"\bfloat2\s*\(\s*" + Number + @"\s*,\s*" + Number + @"\s*\)")]
    private static partial Regex Literal();

    /// <summary>What the literal is given to straight after <c>=</c>: <c>float2 p</c>, <c>let p</c>, or a bare name.</summary>
    [GeneratedRegex(@"(?:\b(?:[A-Za-z_]\w*|let|var)\s+)?([A-Za-z_]\w*)\s*=\s*$")]
    private static partial Regex Assigned();

    [GeneratedRegex(@"([A-Za-z_]\w*)\s*$")]
    private static partial Regex Callee();

    /// <summary>
    /// The points in the code. <paramref name="parameterNames"/> gives a call's parameter names at an
    /// argument's place (<see cref="SlangCompletions.ParameterNames"/>); a call it knows nothing about
    /// gives no point.
    /// </summary>
    public static List<PointLiteral> Find(IReadOnlyList<string> lines, Func<string, int, IReadOnlyList<string>> parameterNames)
    {
        var found = new List<PointLiteral>();
        for (int line = 0; line < lines.Count; line++)
        {
            string text = lines[line];
            int comment = text.IndexOf("//", StringComparison.Ordinal);
            foreach (Match match in Literal().Matches(text))
            {
                if (comment >= 0 && match.Index >= comment)
                    break;
                // The whole argument or value: nothing after it but the next argument or the end.
                string after = text[(match.Index + match.Length)..].TrimStart();
                if (after.Length > 0 && after[0] is not (',' or ')' or ';'))
                    continue;
                if (NameOf(text[..match.Index], parameterNames) is not { } name)
                    continue;
                var value = new Vector2(Parse(match.Groups[1].Value), Parse(match.Groups[2].Value));
                found.Add(new PointLiteral(line, match.Index, match.Index + match.Length, value, name));
            }
        }

        return found;
    }

    /// <summary>The point under a caret, or null.</summary>
    public static PointLiteral? At(IReadOnlyList<string> lines, int line, int column, Func<string, int, IReadOnlyList<string>> parameterNames)
    {
        if (line < 0 || line >= lines.Count)
            return null;
        foreach (PointLiteral point in Find([lines[line]], parameterNames))
        {
            PointLiteral placed = point with { Line = line };
            if (placed.Holds(line, column))
                return placed;
        }

        return null;
    }

    /// <summary>Whether a variable or uniform's name reads as a place: not a size, a direction, a range or the like.</summary>
    public static bool IsPlaceName(string name)
    {
        if (name is "from" or "to")
            return false;
        string lower = name.ToLowerInvariant();
        return !NotPlaces.Any(word => lower.Contains(word, StringComparison.Ordinal));
    }

    /// <summary>The literal's text for a new value, whole pixels: <c>float2(40, -12)</c>.</summary>
    public static string Text(Vector2 value) =>
        string.Create(CultureInfo.InvariantCulture, $"float2({(int)MathF.Round(value.X)}, {(int)MathF.Round(value.Y)})");

    /// <summary>What a literal starting after <paramref name="before"/> is given to, when that's a place; null otherwise.</summary>
    private static string? NameOf(string before, Func<string, int, IReadOnlyList<string>> parameterNames)
    {
        // The bracket it sits in, and which argument it is there.
        int depth = 0, index = 0, open = -1;
        for (int i = before.Length - 1; i >= 0 && open < 0; i--)
        {
            char c = before[i];
            if (c is ')' or ']')
                depth++;
            else if (c is '(' or '[' && depth > 0)
                depth--;
            else if (c == '(')
                open = i;
            else if (c == '[')
                return null;
            else if (c == ',' && depth == 0)
                index++;
        }

        if (open < 0)
        {
            // Not an argument: given to a variable, or nothing we know.
            Match assigned = Assigned().Match(before);
            return assigned.Success && IsPlaceName(assigned.Groups[1].Value) ? assigned.Groups[1].Value : null;
        }

        // Straight inside the call's bracket, not part of a sum in it (float2(1, 2) * 3 is worked out).
        string between = before[(open + 1)..];
        int lastComma = between.LastIndexOf(',');
        if (between[(lastComma + 1)..].Trim().Length > 0)
            return null;

        Match callee = Callee().Match(before[..open]);
        if (!callee.Success)
            return null;
        IReadOnlyList<string> names = parameterNames(callee.Groups[1].Value, index);
        return names.Count > 0 && names.All(Places.Contains) ? names[0] : null;
    }

    private static float Parse(string number) => float.Parse(number, NumberStyles.Float, CultureInfo.InvariantCulture);
}

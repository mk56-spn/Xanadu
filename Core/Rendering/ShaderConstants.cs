using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Xanadu.Rendering;

/// <summary>
/// The shader library's named constants (<c>color.gold</c>, <c>PI</c>, …) as
/// numbers, so a <c>[Default(...)]</c> can name one. Slang's attributes only
/// take literals, and the host reads defaults from reflection before anything
/// runs, so names are swapped for their numbers in the source first
/// (<see cref="ExpandDefaults"/>), and read the same way wherever defaults are
/// read from text. The host loads the table from the library at start
/// (<see cref="Use"/>); until then it is empty and names stay as they are.
/// </summary>
public sealed partial class ShaderConstants
{
    private readonly Dictionary<string, float[]> _values = new(StringComparer.Ordinal);

    /// <summary>The running host's table; empty where nothing loaded one (tests).</summary>
    public static ShaderConstants Library { get; private set; } = new();

    public static void Use(ShaderConstants constants) => Library = constants ?? throw new ArgumentNullException(nameof(constants));

    public int Count => _values.Count;

    [GeneratedRegex(@"namespace\s+(\w+)\s*\{")]
    private static partial Regex Namespace();

    [GeneratedRegex(@"public\s+static\s+const\s+(?:float[234]?|int[234]?)\s+(\w+)\s*=\s*([^;]+);")]
    private static partial Regex Constant();

    [GeneratedRegex(@"\[\s*Default\s*\(((?:[^()]|\([^()]*\))*)\)\s*\]")]
    private static partial Regex DefaultTag();

    /// <summary>Every <c>public static const</c> with literal numbers in the given library modules, named as code names them.</summary>
    public static ShaderConstants FromSources(IEnumerable<string> sources)
    {
        var constants = new ShaderConstants();
        foreach (string source in sources)
            constants.Read(source);
        return constants;
    }

    /// <summary>The numbers a name stands for (<c>color.gold</c> or <c>color::gold</c>), if the library has it.</summary>
    public bool TryGet(string name, out float[] values) =>
        _values.TryGetValue(name.Replace("::", ".").Replace(" ", ""), out values!);

    /// <summary>
    /// A default's arguments as numbers: each one a literal or a library name
    /// (which may stand for several numbers), at most four in all. Null when an
    /// argument is neither.
    /// </summary>
    public float[]? Numbers(string arguments)
    {
        var numbers = new List<float>();
        foreach (string part in arguments.Split(','))
        {
            string argument = part.Trim();
            if (argument.Length == 0)
                continue;
            if (TryGet(argument, out float[] named))
                numbers.AddRange(named);
            else if (float.TryParse(argument.TrimEnd('f', 'F'), NumberStyles.Float, CultureInfo.InvariantCulture, out float number))
                numbers.Add(number);
            else
                return null;
        }

        return numbers.Count > 4 ? numbers.GetRange(0, 4).ToArray() : numbers.ToArray();
    }

    /// <summary>Rewrites every <c>[Default(...)]</c> that names a library constant into plain numbers; others are left alone.</summary>
    public string ExpandDefaults(string source) =>
        DefaultTag().Replace(source, match =>
        {
            string arguments = match.Groups[1].Value;
            if (!HasName(arguments) || Numbers(arguments) is not { } numbers)
                return match.Value;
            return "[Default(" + string.Join(", ", Array.ConvertAll(numbers, n => n.ToString("R", CultureInfo.InvariantCulture))) + ")]";
        });

    private static bool HasName(string arguments) =>
        Array.Exists(arguments.Split(','), part => part.Trim() is { Length: > 0 } a && (char.IsLetter(a[0]) || a[0] == '_'));

    private void Read(string source)
    {
        var scopes = Scopes(source);
        foreach (Match match in Constant().Matches(source))
        {
            if (Literals(match.Groups[2].Value) is not { } numbers)
                continue;
            string? scope = null;
            foreach ((int start, int end, string name) in scopes)
            {
                if (match.Index > start && match.Index < end)
                    scope = name;
            }

            _values[scope is null ? match.Groups[1].Value : scope + "." + match.Groups[1].Value] = numbers;
        }
    }

    /// <summary>Each namespace's name and the span of its braces.</summary>
    private static List<(int Start, int End, string Name)> Scopes(string source)
    {
        var scopes = new List<(int, int, string)>();
        foreach (Match match in Namespace().Matches(source))
        {
            int open = match.Index + match.Length - 1, depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '{')
                    depth++;
                else if (source[i] == '}' && --depth == 0)
                {
                    scopes.Add((open, i, match.Groups[1].Value));
                    break;
                }
            }
        }

        return scopes;
    }

    /// <summary>The numbers in <c>3.14</c> or <c>float4(1.0, 0.84, 0.0, 1.0)</c>; null for anything else.</summary>
    private static float[]? Literals(string value)
    {
        value = value.Trim();
        int open = value.IndexOf('(');
        string inside = open >= 0 && value.EndsWith(')') ? value[(open + 1)..^1] : value;
        var numbers = new List<float>();
        foreach (string part in inside.Split(','))
        {
            if (!float.TryParse(part.Trim().TrimEnd('f', 'F'), NumberStyles.Float, CultureInfo.InvariantCulture, out float number))
                return null;
            numbers.Add(number);
        }

        // float4(0.5) fills every channel.
        if (open >= 0 && numbers.Count == 1 && char.IsDigit(value[open - 1]))
        {
            int channels = value[open - 1] - '0';
            return Array.ConvertAll(new float[channels], _ => numbers[0]);
        }

        return numbers.ToArray();
    }
}

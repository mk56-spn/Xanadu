using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Xanadu.Rendering;

/// <summary>
/// What an effect's <c>shade</c> asks for. It returns <c>float4</c> and takes
/// any of <c>Canvas</c> (the quad under the pixel, usually all it needs) and
/// <c>ItemInput</c> (everything else: uv, the item's colour, screen place), in
/// any order and under any names, or nothing at all:
/// <code>
/// float4 shade(Canvas canvas)
/// float4 shade(ItemInput i, Canvas canvas)
/// </code>
/// The host's wrapper calls it with what it asked for, from the pixel's <c>ItemInput i</c>.
/// </summary>
public static partial class ShadeSignature
{
    /// <summary>The usual way to write it, for messages and new effects.</summary>
    public const string Usual = "float4 shade(Canvas canvas)";

    [GeneratedRegex(@"\bfloat4\s+shade\s*\(([^)]*)\)")]
    private static partial Regex Shade();

    [GeneratedRegex(@"^\s*(?:in\s+)?(\w+)\s+\w+\s*$")]
    private static partial Regex Parameter();

    /// <summary>What each parameter type is passed, from the wrapper's <c>ItemInput i</c>.</summary>
    private static readonly Dictionary<string, string> Passed = new()
    {
        ["ItemInput"] = "i",
        ["Canvas"] = "i.canvas",
    };

    /// <summary>
    /// The arguments the wrapper passes <c>shade</c> (<c>"i.canvas"</c>, <c>"i, i.canvas"</c>, <c>""</c>),
    /// or null when the source has no <c>float4 shade(…)</c> or it asks for something else;
    /// <paramref name="problem"/> says which.
    /// </summary>
    public static string? Arguments(string source, out string problem)
    {
        string code = Regex.Replace(Regex.Replace(source, @"/\*.*?\*/", " ", RegexOptions.Singleline), @"//[^\n]*", "");
        Match shade = Shade().Match(code);
        if (!shade.Success)
        {
            problem = $"A shader must define \"{Usual}\" (it may take ItemInput too).";
            return null;
        }

        var arguments = new List<string>();
        string parameters = shade.Groups[1].Value;
        if (parameters.Trim().Length > 0)
        {
            foreach (string parameter in parameters.Split(','))
            {
                Match match = Parameter().Match(parameter);
                if (!match.Success || !Passed.TryGetValue(match.Groups[1].Value, out string? passed))
                {
                    problem = $"shade can take Canvas and ItemInput, not \"{parameter.Trim()}\".";
                    return null;
                }

                arguments.Add(passed);
            }
        }

        problem = "";
        return string.Join(", ", arguments);
    }
}

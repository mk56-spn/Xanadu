using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>Where a colour constant is named in a line, and the colour it holds (straight rgba).</summary>
public readonly record struct ColourUse(int Column, int Length, Vector4 Colour);

/// <summary>
/// Colour constants whose value is plain numbers, so the editor can show a
/// swatch beside them: the library's (<c>colours.gold</c>, by namespace and
/// name) and the ones a shader declares itself
/// (<c>static const colour sky = float4(0.4, 0.6, 1.0, 1.0);</c>, by bare name).
/// A constant made from anything but literal numbers has no swatch. A
/// <c>colour(...)</c> made of literal numbers gets one too, wherever it is
/// written (<c>colour(1.0, 0.3, 1.0, 0.5)</c>, <c>colour(1.0, 0.3, 1.0)</c> opaque, or <c>colour(0.5)</c> splatted).
/// </summary>
public sealed partial class SlangColours
{
    [GeneratedRegex(@"static\s+const\s+(?:colour|float4|float3)\s+(\w+)\s*=\s*(?:colour|float4|float3)\s*\(([^()]*)\)")]
    private static partial Regex Constant();

    [GeneratedRegex(@"\bcolour\s*\(([^()]*)\)")]
    private static partial Regex Literal();

    [GeneratedRegex(@"public\s+namespace\s+(\w+)")]
    private static partial Regex NamespaceDeclaration();

    [GeneratedRegex(@"\b([A-Za-z_]\w*)(?:\s*\.\s*([A-Za-z_]\w*))?")]
    private static partial Regex Name();

    /// <summary>The library's constants by qualified name (<c>colours.gold</c>), or bare name outside a namespace.</summary>
    private readonly Dictionary<string, Vector4> _library = new(StringComparer.Ordinal);

    public int Count => _library.Count;

    /// <summary>Every colour constant in the <c>.slang</c> files under a folder.</summary>
    public static SlangColours FromLibrary(string folder)
    {
        var colours = new SlangColours();
        if (Directory.Exists(folder))
        {
            foreach (string file in Directory.EnumerateFiles(folder, "*.slang", SearchOption.AllDirectories))
                colours.Read(File.ReadAllText(file));
        }

        return colours;
    }

    /// <summary>Adds the constants in one library file, named under the namespace they sit in.</summary>
    public void Read(string source)
    {
        var namespaces = new List<(int Index, string Name)>();
        foreach (Match match in NamespaceDeclaration().Matches(source))
            namespaces.Add((match.Index, match.Groups[1].Value));
        foreach (Match match in Constant().Matches(source))
        {
            if (Parse(match.Groups[2].Value) is not { } colour)
                continue;
            string? owner = null;
            foreach (var declared in namespaces)
            {
                if (declared.Index < match.Index)
                    owner = declared.Name;
            }

            _library[owner is null ? match.Groups[1].Value : owner + "." + match.Groups[1].Value] = colour;
        }
    }

    /// <summary>The colour constants a shader declares itself, by bare name.</summary>
    public static Dictionary<string, Vector4> Declared(IReadOnlyList<string> lines)
    {
        var declared = new Dictionary<string, Vector4>(StringComparer.Ordinal);
        foreach (string line in lines)
        {
            foreach (Match match in Constant().Matches(line))
            {
                if (Parse(match.Groups[2].Value) is { } colour)
                    declared[match.Groups[1].Value] = colour;
            }
        }

        return declared;
    }

    /// <summary>The colour constants named in one line of code, left to right; comments are skipped.</summary>
    public List<ColourUse> In(string line, IReadOnlyDictionary<string, Vector4> declared)
    {
        var uses = new List<ColourUse>();
        int comment = line.IndexOf("//", StringComparison.Ordinal);
        string code = comment >= 0 ? line[..comment] : line;
        foreach (Match match in Name().Matches(code))
        {
            // A member of something else (shape.colours.x) isn't the library's.
            if (match.Index > 0 && code[match.Index - 1] == '.')
                continue;
            string first = match.Groups[1].Value;
            if (match.Groups[2].Success && _library.TryGetValue(first + "." + match.Groups[2].Value, out Vector4 qualified))
                uses.Add(new ColourUse(match.Index, match.Length, qualified));
            else if (!match.Groups[2].Success && (declared.TryGetValue(first, out Vector4 colour) || _library.TryGetValue(first, out colour)))
                uses.Add(new ColourUse(match.Index, match.Length, colour));
        }

        // A constant's own declaration already has its swatch on the name.
        var constants = Constant().Matches(code);
        foreach (Match match in Literal().Matches(code))
        {
            if (match.Index > 0 && code[match.Index - 1] == '.')
                continue;
            bool declaration = false;
            foreach (Match constant in constants)
                declaration |= match.Index >= constant.Index && match.Index < constant.Index + constant.Length;
            if (!declaration && ParseLiteral(match.Groups[1].Value) is { } literal)
                uses.Add(new ColourUse(match.Index, match.Length, literal));
        }

        uses.Sort((a, b) => a.Column.CompareTo(b.Column));
        return uses;
    }

    /// <summary>A <c>colour(...)</c>'s arguments as rgba: four numbers, three (opaque), or one splatted to all four; null for anything else.</summary>
    private static Vector4? ParseLiteral(string arguments)
    {
        if (arguments.Split(',').Length == 1)
            return float.TryParse(arguments.Trim().TrimEnd('f', 'F'), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? new Vector4(v) : null;
        return Parse(arguments);
    }

    /// <summary>Three or four literal numbers as rgba (alpha 1 when left off); null for anything else.</summary>
    private static Vector4? Parse(string arguments)
    {
        string[] parts = arguments.Split(',');
        if (parts.Length is not (3 or 4))
            return null;
        Span<float> values = stackalloc float[4];
        values[3] = 1;
        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i].Trim().TrimEnd('f', 'F');
            if (!float.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
                return null;
        }

        return new Vector4(values[0], values[1], values[2], values[3]);
    }
}

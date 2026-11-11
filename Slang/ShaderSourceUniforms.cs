using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>
/// One keyable shader setting: a global <c>uniform</c> the effect tags
/// <c>[Instance]</c>, with the value its <c>[Default(...)]</c> gives it (zero
/// when it gives none). An unkeyed setting holds <paramref name="Default"/>, so
/// a first key starts there.
/// </summary>
public readonly record struct InstanceUniform(string Name, int Channels, Vector4 Default = default);

/// <summary>
/// Reads the keyable settings out of a level shader's Slang source
/// (Docs/render-architecture.md §5.3, step 8; Docs/host.md §5.5):
/// global uniforms tagged <c>[Instance]</c>, such as
/// <c>[Instance] [Default(0.5)] uniform float strength;</c>. Those are what a
/// group's item-shader track can key; an untagged <c>uniform</c> stays a tuned
/// constant. Defaults are taken as written, missing channels zero, as the host
/// reads them from Slang's reflection.
/// </summary>
public static partial class ShaderSourceUniforms
{
    [GeneratedRegex(
        @"((?:\[\s*\w+\s*(?:\((?:[^()]|\([^()]*\))*\))?\s*\]\s*)+)uniform\s+(float|int|uint|float2|float3|float4|colour)\s+([A-Za-z_][A-Za-z0-9_]*)\s*;",
        RegexOptions.Singleline)]
    private static partial Regex Declared();

    [GeneratedRegex(@"\[\s*Instance\s*\]")]
    private static partial Regex InstanceTag();

    [GeneratedRegex(@"\[\s*Default\s*\(((?:[^()]|\([^()]*\))*)\)\s*\]")]
    private static partial Regex DefaultTag();

    [GeneratedRegex(@"//[^\n]*|/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex Comments();

    /// <summary>The keyable settings a source declares, in declaration order.</summary>
    public static List<InstanceUniform> InstanceUniforms(string? source)
    {
        var uniforms = new List<InstanceUniform>();
        if (string.IsNullOrEmpty(source))
            return uniforms;

        foreach (Match match in Declared().Matches(Comments().Replace(source, "")))
        {
            string attributes = match.Groups[1].Value;
            if (!InstanceTag().IsMatch(attributes))
                continue;

            string name = match.Groups[3].Value;
            if (uniforms.Exists(uniform => uniform.Name == name))
                continue;

            Match fallback = DefaultTag().Match(attributes);
            uniforms.Add(new InstanceUniform(
                name,
                Channels(match.Groups[2].Value),
                fallback.Success ? Default(fallback.Groups[1].Value) : Vector4.Zero));
        }

        return uniforms;
    }

    /// <summary>Up to four numbers or library names (<c>color.gold</c>, <see cref="ShaderConstants"/>); anything else reads as zero.</summary>
    private static Vector4 Default(string text)
    {
        if (ShaderConstants.Library.Numbers(text) is not { } numbers)
            return Vector4.Zero;
        float At(int i) => i < numbers.Length ? numbers[i] : 0f;
        return new Vector4(At(0), At(1), At(2), At(3));
    }

    private static int Channels(string type) => type switch
    {
        "float2" => 2,
        "float3" => 3,
        "float4" or "colour" => 4,
        _ => 1,
    };
}

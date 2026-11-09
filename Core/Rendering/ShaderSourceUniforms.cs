using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Godot;

namespace Xanadu.Rendering;

/// <summary>
/// One keyable shader setting: a global <c>uniform</c> the effect tags
/// <c>[Instance]</c>, with the value its <c>[Default(...)]</c> gives it (zero
/// when it gives none). An unkeyed setting holds <paramref name="Default"/>, so
/// a first key starts there.
/// </summary>
public readonly record struct InstanceUniform(string Name, int Channels, Vector4 Default = default);

/// <summary>
/// Reads the keyable settings out of a level shader's Slang source
/// (docs/render-architecture.md §5.3, step 8; docs/moonworks-port.md step 5):
/// global uniforms tagged <c>[Instance]</c>, such as
/// <c>[Instance] [Default(0.5)] uniform float strength;</c>. Those are what a
/// group's item-shader track can key; an untagged <c>uniform</c> stays a tuned
/// constant. Defaults are taken as written, missing channels zero, as the host
/// reads them from Slang's reflection.
/// </summary>
public static partial class ShaderSourceUniforms
{
    [GeneratedRegex(
        @"((?:\[\s*\w+\s*(?:\((?:[^()]|\([^()]*\))*\))?\s*\]\s*)+)uniform\s+(float|int|uint|float2|float3|float4)\s+([A-Za-z_][A-Za-z0-9_]*)\s*;",
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

    /// <summary>Up to four comma-separated numbers; anything that isn't a number reads as zero.</summary>
    private static Vector4 Default(string text)
    {
        string[] parts = text.Split(',');
        float[] values = new float[4];
        for (int i = 0; i < parts.Length && i < 4; i++)
        {
            if (!float.TryParse(parts[i].Trim().TrimEnd('f', 'F'), NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
                return Vector4.Zero;
        }

        return new Vector4(values[0], values[1], values[2], values[3]);
    }

    private static int Channels(string type) => type switch
    {
        "float2" => 2,
        "float3" => 3,
        "float4" => 4,
        _ => 1,
    };
}

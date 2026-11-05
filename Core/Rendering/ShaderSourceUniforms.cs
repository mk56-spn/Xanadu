using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Xanadu.Rendering;

/// <summary>One keyable shader setting: an <c>instance uniform</c> the shader declares.</summary>
public readonly record struct InstanceUniform(string Name, int Channels);

/// <summary>
/// Reads the <c>instance uniform</c> declarations out of a canvas shader's
/// source (docs/render-architecture.md §5.3, step 8). Those are the settings a
/// group's item-shader track can key; a plain <c>uniform</c> stays a tuned
/// constant.
/// </summary>
public static partial class ShaderSourceUniforms
{
    [GeneratedRegex(
        @"\binstance\s+uniform\s+(?:(?:highp|mediump|lowp)\s+)?(float|int|vec2|vec3|vec4)\s+([A-Za-z_][A-Za-z0-9_]*)\s*;",
        RegexOptions.Singleline)]
    private static partial Regex Declared();

    [GeneratedRegex(@"//[^\n]*|/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex Comments();

    /// <summary>The instance uniforms a source declares, in declaration order.</summary>
    public static List<InstanceUniform> InstanceUniforms(string? source)
    {
        var uniforms = new List<InstanceUniform>();
        if (string.IsNullOrEmpty(source))
            return uniforms;

        foreach (Match match in Declared().Matches(Comments().Replace(source, "")))
        {
            string name = match.Groups[2].Value;
            if (uniforms.Exists(uniform => uniform.Name == name))
                continue;
            uniforms.Add(new InstanceUniform(name, Channels(match.Groups[1].Value)));
        }

        return uniforms;
    }

    private static int Channels(string type) => type switch
    {
        "vec2" => 2,
        "vec3" => 3,
        "vec4" => 4,
        _ => 1,
    };
}

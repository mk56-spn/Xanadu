using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Godot;

namespace Xanadu.Rendering;

/// <summary>
/// One keyable shader setting: an <c>instance uniform</c> the shader declares,
/// with the value its declaration gives it (zero when it gives none). An
/// unkeyed setting holds <paramref name="Default"/>, so a first key starts there.
/// </summary>
public readonly record struct InstanceUniform(string Name, int Channels, Vector4 Default = default);

/// <summary>
/// Reads the <c>instance uniform</c> declarations out of a canvas shader's
/// source (docs/render-architecture.md §5.3, step 8). Those are the settings a
/// group's item-shader track can key; a plain <c>uniform</c> stays a tuned
/// constant.
/// </summary>
public static partial class ShaderSourceUniforms
{
    [GeneratedRegex(
        @"\binstance\s+uniform\s+(?:(?:highp|mediump|lowp)\s+)?(float|int|vec2|vec3|vec4)\s+([A-Za-z_][A-Za-z0-9_]*)\s*(?::[^=;]*)?(?:=\s*([^;]+?)\s*)?;",
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
            int channels = Channels(match.Groups[1].Value);
            uniforms.Add(new InstanceUniform(name, channels, Default(match.Groups[3].Value, channels)));
        }

        return uniforms;
    }

    /// <summary>
    /// The declared value: a number, or <c>vecN(...)</c> with one value (spread
    /// over every channel) or one per channel. Anything else reads as zero.
    /// </summary>
    private static Vector4 Default(string text, int channels)
    {
        text = text.Trim();
        if (text.Length == 0)
            return Vector4.Zero;

        int open = text.IndexOf('(');
        if (open >= 0)
        {
            if (!text.EndsWith(')'))
                return Vector4.Zero;
            text = text[(open + 1)..^1];
        }

        string[] parts = text.Split(',');
        float[] values = new float[4];
        for (int i = 0; i < parts.Length && i < 4; i++)
        {
            if (!float.TryParse(parts[i].Trim().TrimEnd('f', 'F'), NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
                return Vector4.Zero;
        }

        if (parts.Length == 1)
        {
            for (int i = 1; i < channels; i++)
                values[i] = values[0];
        }

        return new Vector4(values[0], values[1], values[2], values[3]);
    }

    private static int Channels(string type) => type switch
    {
        "vec2" => 2,
        "vec3" => 3,
        "vec4" => 4,
        _ => 1,
    };
}

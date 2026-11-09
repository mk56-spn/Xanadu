using System.Text.RegularExpressions;
using Xanadu.Ecs;

namespace Xanadu.Levels;

/// <summary>
/// The cheap checks a level's shader source must pass before it is kept: a Slang
/// fragment effect against <c>xanadu.stage</c> (docs/moonworks-port.md §2a) that
/// defines <c>float4 shade(ItemInput i)</c>. Whether it really compiles is only
/// known when the host builds it; a broken one draws plain and logs Slang's error.
/// </summary>
public static partial class ShaderSourceRules
{
    // The effect's entry: comments may sit anywhere before it.
    [GeneratedRegex(@"\bfloat4\s+shade\s*\(\s*ItemInput\b")]
    private static partial Regex ShadeFunction();

    [GeneratedRegex(@"\bshader_type\s+\w+\s*;")]
    private static partial Regex GodotHeader();

    /// <summary>Whether the source is something a level can use; <paramref name="problem"/> says why not.</summary>
    public static bool IsAcceptable(string? source, out string problem)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            problem = "The shader is empty.";
            return false;
        }

        if (source.Length > ShaderLimits.MaxSourceChars)
        {
            problem = $"The shader is longer than {ShaderLimits.MaxSourceChars:N0} characters.";
            return false;
        }

        if (GodotHeader().IsMatch(source))
        {
            problem = "Godot shaders are no longer supported: write a Slang effect with \"float4 shade(ItemInput i)\".";
            return false;
        }

        if (!ShadeFunction().IsMatch(source))
        {
            problem = "A shader must define \"float4 shade(ItemInput i)\".";
            return false;
        }

        problem = "";
        return true;
    }
}

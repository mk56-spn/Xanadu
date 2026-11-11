using System.Text.RegularExpressions;
using Xanadu.Ecs;
using Xanadu.Rendering;

namespace Xanadu.Levels;

/// <summary>
/// The cheap checks a level's shader source must pass before it is kept: a Slang
/// fragment effect against <c>xanadu.stage</c> (Docs/host.md §3) that
/// defines a <c>shade</c> the host can call (<see cref="ShadeSignature"/>). Whether it really compiles is only
/// known when the host builds it; a broken one draws plain and logs Slang's error.
/// </summary>
public static partial class ShaderSourceRules
{
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
            problem = $"Godot shaders are no longer supported: write a Slang effect with \"{ShadeSignature.Usual}\".";
            return false;
        }

        return ShadeSignature.Arguments(source, out problem) is not null;
    }
}

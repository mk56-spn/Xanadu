using System.Text.RegularExpressions;
using Xanadu.Ecs;

namespace Xanadu.Levels;

/// <summary>What shader source a level may carry. Levels come from other people, so it is checked on load.</summary>
public static partial class ShaderSourceRules
{
    // Comments and blank space may come first; the first statement must declare a canvas shader.
    [GeneratedRegex(@"\A(?:\s|//[^\n]*(?:\n|\z)|/\*.*?\*/)*shader_type\s+canvas_item\s*;", RegexOptions.Singleline)]
    private static partial Regex CanvasItemHeader();

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

        if (!CanvasItemHeader().IsMatch(source))
        {
            problem = "Only canvas shaders are supported: start with \"shader_type canvas_item;\".";
            return false;
        }

        problem = "";
        return true;
    }
}

using System;

namespace Xanadu.Rendering;

/// <summary>
/// How shader tuners guess what a value is for when the shader gives no hint:
/// a four-channel value named like a colour is a colour, and a slider spans a
/// few times the value's size. Shared by the F2 tuner and Labs' shader editor.
/// </summary>
public static class UniformRanges
{
    /// <summary>Whether a value's name says it is a colour (Godot's <c>source_color</c> hint, by name).</summary>
    public static bool IsColourName(string name) =>
        name.Contains("color", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("colour", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("tint", StringComparison.OrdinalIgnoreCase);

    /// <summary>A slider range round a value: 0..1 for colours, else up to four times its size (at least 1), below zero when it is negative.</summary>
    public static (float Min, float Max) Guess(bool colour, float reference)
    {
        if (colour)
            return (0, 1);

        float span = MathF.Max(1f, MathF.Abs(reference) * 4f);
        return (reference < 0 ? -span : 0, span);
    }
}

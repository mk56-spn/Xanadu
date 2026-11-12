using System;
using System.Diagnostics;

namespace Xanadu.Rendering;

/// <summary>
/// The play keys as shaders see them (<c>keys.</c> in <c>xanadu.anim</c>): when
/// each was last pressed. Whoever reads the keys marks a press here (the game's
/// play input, the lab's shader editor); the renderer hands every shader the
/// seconds since each, once a frame. Keys are numbered as the game's
/// <c>KeybindAction</c>: left, right, up, down, then the three action keys.
/// </summary>
public static class ShaderKeys
{
    public const int Count = 7;

    /// <summary>The first action key; the ones before it are the directions.</summary>
    public const int FirstAction = 4;

    /// <summary>What a key never pressed reads as: long enough ago for any clip to have finished.</summary>
    public const float Never = 1e6f;

    private static readonly long?[] PressedAt = new long?[Count];

    /// <summary>A key went down this frame.</summary>
    public static void Press(int key)
    {
        if (key >= 0 && key < Count)
            PressedAt[key] = Stopwatch.GetTimestamp();
    }

    /// <summary>Seconds since the key was last pressed, or <see cref="Never"/>.</summary>
    public static float Since(int key) =>
        key >= 0 && key < Count && PressedAt[key] is { } at
            ? (float)Math.Min(Never, Stopwatch.GetElapsedTime(at).TotalSeconds)
            : Never;

    /// <summary>Forgets every press (a new level, a test).</summary>
    public static void Clear() => Array.Clear(PressedAt);
}

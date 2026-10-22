using System;
using Writer.Ecs;

namespace Writer.Systems.Editor;

/// <summary>Mouse-wheel timeline seeking.</summary>
public static class EditorScroll
{
    public static long Target(
        Playhead playhead,
        int direction,
        bool fast = false)
    {
        long step = fast ? 2_000 : 500;
        return Math.Clamp(
            playhead.TimeMs + direction * step,
            0,
            Math.Max(0, playhead.DurationMs));
    }
}

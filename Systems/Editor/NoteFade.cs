using System;
using Writer.Ecs;

namespace Writer.Systems.Editor;

/// <summary>AR9-style note alpha: 600 ms in, 600 ms out by default.</summary>
public static class NoteFade
{
    public static float Alpha(Note note, long playheadMs)
    {
        long delta = note.TimeMs - playheadMs;

        if (delta > note.FadeInMs || delta < -note.FadeOutMs)
            return 0f;

        float alpha = delta >= 0
            ? 1f - delta / (float)Math.Max(1, note.FadeInMs)
            : 1f + delta / (float)Math.Max(1, note.FadeOutMs);

        return Math.Clamp(alpha, 0f, 1f);
    }
}

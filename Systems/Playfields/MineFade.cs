using System;
using Writer.Ecs;

namespace Writer.Systems.Playfields;

/// <summary>Visibility window for mine hazards.</summary>
public static class MineFade
{
    public static float Alpha(Mine mine, long playheadMs)
    {
        long delta = mine.TimeMs - playheadMs;

        if (delta > mine.FadeInMs || delta < -mine.FadeOutMs)
            return 0f;

        float alpha = delta >= 0
            ? 1f - delta / (float)Math.Max(1, mine.FadeInMs)
            : 1f + delta / (float)Math.Max(1, mine.FadeOutMs);

        return Math.Clamp(alpha, 0f, 1f);
    }
}

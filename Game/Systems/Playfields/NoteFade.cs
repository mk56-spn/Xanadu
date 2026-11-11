using System;
using Xanadu.Ecs;

namespace Xanadu.Systems.Playfields;

/// <summary>Shared presentation timing for every play note.</summary>
public static class NoteFade
{
    /// <summary>Temporary shared fade-in until note variance is reintroduced.</summary>
    public const long FadeInMs = 600;

    /// <summary>Temporary shared fade-out until note variance is reintroduced.</summary>
    public const long FadeOutMs = 300;

    /// <summary>Shared approach-ring duration for every note kind.</summary>
    public const float ApproachWindowMs = 600f;

    public static float Alpha(Note note, long playheadMs) =>
        Alpha(note.TimeMs, playheadMs);

    public static float Alpha(long noteTimeMs, long playheadMs)
    {
        long delta = noteTimeMs - playheadMs;

        if (delta > FadeInMs || delta < -FadeOutMs)
            return 0f;

        float alpha = delta >= 0
            ? 1f - delta / (float)FadeInMs
            : 1f + delta / (float)FadeOutMs;

        return Math.Clamp(alpha, 0f, 1f);
    }

    public static float Approach(long noteTimeMs, long playheadMs)
    {
        long delta = noteTimeMs - playheadMs;
        if (delta < 0 || delta > ApproachWindowMs)
            return -1f;

        return delta / ApproachWindowMs;
    }
}

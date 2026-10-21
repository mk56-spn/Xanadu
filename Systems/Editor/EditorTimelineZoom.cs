using System;

namespace Writer.Systems.Editor;

/// <summary>Timeline zoom levels and the default six-second view.</summary>
public static class EditorTimelineZoom
{
    public const float TargetSeconds = 6f;

    public static readonly float[] Levels = { 1f, 2f, 4f, 8f, 16f, 32f };

    public static float ClosestForDuration(long durationMs)
    {
        if (durationMs <= 0)
            return Levels[0];

        float best = Levels[0];
        float bestDistance = float.MaxValue;

        foreach (float level in Levels)
        {
            float visibleSeconds = durationMs / 1000f / level;
            float distance = Math.Abs(visibleSeconds - TargetSeconds);

            if (distance >= bestDistance)
                continue;

            best = level;
            bestDistance = distance;
        }

        return best;
    }

    public static float Step(float current, int direction)
    {
        int index = 0;
        for (int i = 0; i < Levels.Length; i++)
        {
            if (Math.Abs(Levels[i] - current) < 0.01f)
            {
                index = i;
                break;
            }
        }

        index = Math.Clamp(index + Math.Sign(direction), 0, Levels.Length - 1);
        return Levels[index];
    }
}

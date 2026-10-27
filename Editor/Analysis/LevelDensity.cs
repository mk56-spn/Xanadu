using System;
using System.Collections.Generic;
using Godot;

namespace Xanadu.Editor.Analysis;

/// <summary>
/// How busy a chart is over time: notes per second, and how far the player
/// must travel per second to reach them. Each array is one value per
/// <see cref="BucketMs"/> of song time.
/// </summary>
public sealed record DensityProfile(
    int BucketMs,
    float[] NotesPerSecond,
    float[] TravelPixelsPerSecond,
    float PeakNotes,
    float PeakTravel)
{
    public static readonly DensityProfile Empty = new(500, [], [], 0, 0);

    public int BucketCount => NotesPerSecond.Length;
}

public static class LevelDensity
{
    public const int DefaultBucketMs = 500;

    /// <summary>
    /// Buckets the notes by time. <paramref name="notes"/> need not be sorted.
    /// Travel is the straight-line distance from each note to the one before
    /// it, credited to the later note's bucket.
    /// </summary>
    public static DensityProfile Compute(
        IReadOnlyList<AnalysedNote> notes,
        long durationMs,
        int bucketMs = DefaultBucketMs)
    {
        if (notes.Count == 0 && durationMs <= 0)
            return DensityProfile.Empty with { BucketMs = bucketMs };

        var sorted = new List<AnalysedNote>(notes);
        sorted.Sort(static (a, b) => a.TimeMs.CompareTo(b.TimeMs));

        long end = Math.Max(durationMs, sorted.Count > 0 ? sorted[^1].TimeMs : 0);
        int count = (int)(Math.Max(0, end) / bucketMs) + 1;
        var counts = new float[count];
        var travel = new float[count];

        for (int i = 0; i < sorted.Count; i++)
        {
            int bucket = BucketOf(sorted[i].TimeMs, bucketMs, count);
            counts[bucket]++;
            if (i > 0)
                travel[bucket] += sorted[i].Position.DistanceTo(sorted[i - 1].Position);
        }

        float perSecond = 1000f / bucketMs;
        float[] notesPerSecond = Smooth(counts, perSecond);
        float[] travelPerSecond = Smooth(travel, perSecond);

        return new DensityProfile(
            bucketMs,
            notesPerSecond,
            travelPerSecond,
            Max(notesPerSecond),
            Max(travelPerSecond));
    }

    private static int BucketOf(long timeMs, int bucketMs, int count) =>
        (int)Math.Clamp(timeMs / bucketMs, 0, count - 1);

    /// <summary>Scales to a per-second rate and softens bucket edges with a 1-2-1 kernel.</summary>
    private static float[] Smooth(float[] values, float scale)
    {
        var result = new float[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            float left = i > 0 ? values[i - 1] : values[i];
            float right = i < values.Length - 1 ? values[i + 1] : values[i];
            result[i] = (left + values[i] * 2 + right) / 4 * scale;
        }

        return result;
    }

    private static float Max(float[] values)
    {
        float max = 0;
        foreach (float value in values)
            max = Math.Max(max, value);

        return max;
    }
}

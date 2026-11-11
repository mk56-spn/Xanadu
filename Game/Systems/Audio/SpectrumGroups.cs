using System;

namespace Xanadu.Systems.Audio;

/// <summary>Collapses band levels into a few broad groups, lowest frequencies first.</summary>
public static class SpectrumGroups
{
    /// <summary>Fills each slot of <paramref name="groups"/> with the mean of an equal share of the bands.</summary>
    public static void Average(ReadOnlySpan<float> bands, Span<float> groups)
    {
        if (groups.IsEmpty)
            return;

        for (int group = 0; group < groups.Length; group++)
        {
            int start = bands.Length * group / groups.Length;
            int end = Math.Max(start + 1, bands.Length * (group + 1) / groups.Length);
            end = Math.Min(end, bands.Length);

            float sum = 0;
            for (int band = start; band < end; band++)
                sum += bands[band];

            groups[group] = end > start ? sum / (end - start) : 0;
        }
    }
}

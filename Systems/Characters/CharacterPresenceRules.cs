using System;
using System.Collections.Generic;
using Xanadu.Animation;
using Xanadu.Ecs;

namespace Xanadu.Systems.Characters;

/// <summary>
/// A character is only on stage near the chart's notes. Once the playhead is more
/// than <see cref="ReachMs"/> from every note, she flashes white and is gone, and a
/// rhombus pulsing on the beat holds her place until the next note is that close
/// again, when it collapses and she flashes back in. It follows the chart, not the
/// player's hits, so a seek or a miss lands in the same look.
/// </summary>
public static class CharacterPresenceRules
{
    /// <summary>How far from the nearest note, before or after, she stays on stage.</summary>
    public const long ReachMs = 1000;

    /// <summary>How long each flash takes: her going white and out, and back in from white.</summary>
    public const long FlashMs = 110;

    /// <summary>How long the rhombus takes to turn in or out; both ease in.</summary>
    public const long RhombusMs = 220;

    /// <summary>Shorter rests than this are skipped, so she never blinks out for a moment.</summary>
    public const long MinAwayMs = 2 * FlashMs + 2 * RhombusMs + 200;

    /// <summary>How much the art's colour is multiplied by at the top of a flash; the art is dark grey, so this is white.</summary>
    public const float FlashLight = 4f;

    /// <summary>How quickly the rhombus's beat pulse dies away; higher is a sharper tap.</summary>
    public const float PulseDecay = 6f;

    /// <summary>
    /// What a character shows this frame. <paramref name="Shown"/> and its
    /// <paramref name="Light"/> (1 is as posed) are the art's; <paramref name="Rhombus"/>
    /// is how open the rhombus is (0 is none) and <paramref name="Pulse"/> its beat, 1 on the beat.
    /// </summary>
    public readonly record struct Look(bool Shown, float Light, float Rhombus, float Pulse)
    {
        public static readonly Look Present = new(true, 1f, 0f, 0f);
    }

    /// <summary>
    /// The stretches she is away for, as (out, in) pairs on the song's clock, from
    /// each note's span (its time to the end of its hold). Before the first note
    /// she is away from the start; after the last she does not come back.
    /// </summary>
    public static long[] AwayWindows(IEnumerable<(long StartMs, long EndMs)> notes)
    {
        var spans = new List<(long Start, long End)>();
        foreach ((long start, long end) in notes)
            spans.Add((start, Math.Max(start, end)));
        if (spans.Count == 0)
            return [];
        spans.Sort((a, b) => a.Start.CompareTo(b.Start));

        var away = new List<long>();
        long reachedTo = long.MinValue;
        foreach ((long start, long end) in spans)
        {
            long outMs = reachedTo == long.MinValue ? long.MinValue : reachedTo + ReachMs;
            long inMs = start - ReachMs;
            if (outMs == long.MinValue || inMs - outMs >= MinAwayMs)
            {
                away.Add(outMs);
                away.Add(inMs);
            }

            reachedTo = Math.Max(reachedTo, end);
        }

        away.Add(reachedTo + ReachMs);
        away.Add(long.MaxValue);
        return away.ToArray();
    }

    /// <summary>The look at <paramref name="songMs"/>, with the rhombus pulsing on the beats of <paramref name="tempo"/>.</summary>
    public static Look At(long songMs, ReadOnlySpan<long> away, Xanadu.Levels.TempoMap tempo)
    {
        for (int i = 0; i + 1 < away.Length; i += 2)
        {
            long outMs = away[i];
            long inMs = away[i + 1];
            if (songMs < outMs)
                break;
            if (songMs >= inMs + FlashMs)
                continue;

            // She comes back from white.
            if (songMs >= inMs)
                return Look.Present with { Light = Light(1f - Part(songMs - inMs, FlashMs)) };

            // She goes white, then out.
            if (outMs != long.MinValue && songMs < outMs + FlashMs)
                return Look.Present with { Light = Light(Part(songMs - outMs, FlashMs)) };

            float open = 1f;
            if (outMs != long.MinValue)
                open = XafEasing.Progress(XafEasePreset.CubicIn, Part(songMs - outMs - FlashMs, RhombusMs));
            if (inMs != long.MaxValue && songMs > inMs - RhombusMs)
                open = Math.Min(open, 1f - XafEasing.Progress(XafEasePreset.CubicIn, Part(songMs - (inMs - RhombusMs), RhombusMs)));

            double beats = tempo.BeatAt(songMs);
            float pulse = MathF.Exp(-(float)(beats - Math.Floor(beats)) * PulseDecay);
            return new Look(false, 1f, open, pulse);
        }

        return Look.Present;
    }

    /// <summary>The look for a player this frame, or <see cref="Look.Present"/> when it has no away windows or no playhead.</summary>
    public static Look For(Friflo.Engine.ECS.Entity player)
    {
        if (!player.TryGetComponent(out CharacterPresence presence) ||
            !player.TryGetComponent(out Playhead playhead))
        {
            return Look.Present;
        }

        return At(playhead.TimeMs, presence.Away, playhead.Tempo);
    }

    private static float Light(float part) =>
        1f + (FlashLight - 1f) * XafEasing.Progress(XafEasePreset.CubicOut, part);

    private static float Part(long elapsedMs, long spanMs) =>
        Math.Clamp(elapsedMs / (float)spanMs, 0f, 1f);
}

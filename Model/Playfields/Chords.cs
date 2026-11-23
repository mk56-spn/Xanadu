using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Settings;

namespace Xanadu.Model.Playfields;

/// <summary>
/// The shape of a chord and where its notes are. A chord has one line per
/// action key, <see cref="Spacing"/> apart along the judgement line, each
/// leaning <see cref="LeanDegrees"/> from the horizontal and falling to the
/// right. A note slides down its key's line for <see cref="ApproachMs"/> and
/// reaches the judgement line on its time. Hitting it does not depend on where
/// the player is: the player's beam drops into the judgement line.
/// </summary>
public static class Chords
{
    public const float LeanDegrees = 50f;

    /// <summary>How far apart the lines meet the judgement line, playfield units.</summary>
    public const float Spacing = 90f;

    /// <summary>How long each line is, from where a note enters to the judgement line.</summary>
    public const float LineLength = 1120f;

    /// <summary>From a note entering its line to its hit time, ms.</summary>
    public const long ApproachMs = 700;

    /// <summary>How long a note stays on the judgement line after its time, fading, ms.</summary>
    public const long LingerMs = 200;

    /// <summary>A note's size: a flat rectangle, as in BMS.</summary>
    public static readonly Vector2 NoteSize = new(Spacing * 0.8f, 18f);

    /// <summary>A dash's size: a thin bar across every line of the chord, overhanging the outer ones a little.</summary>
    public static Vector2 DashSize => new(LineCount * Spacing + 30f, 10f);

    /// <summary>The way down every line, unit length (y points down).</summary>
    public static Vector2 Down
    {
        get
        {
            float angle = LeanDegrees * MathF.PI / 180f;
            return new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        }
    }

    public static int LineCount => KeybindActions.ActionKeys.Length;

    /// <summary>The line a key's notes fall down, or -1 for a key with none.</summary>
    public static int LineOf(KeybindAction key) => Array.IndexOf(KeybindActions.ActionKeys, key);

    /// <summary>Where a line meets the judgement line.</summary>
    public static Vector2 Landing(in Chord chord, int line) => Landing(chord, (float)line);

    /// <summary>Where a lane meets the judgement line; between two lines for a fractional lane.</summary>
    public static Vector2 Landing(in Chord chord, float lane) => chord.Anchor + new Vector2(lane * Spacing, 0f);

    /// <summary>
    /// The lane a note slides down: its key's line, or for a dash the middle of
    /// the chord, so the bar it draws crosses every line. -1 for a key with none
    /// and for an entrance, which stays where it was placed.
    /// </summary>
    public static float LaneOf(in Note note) =>
        note.IsEntrance || note.IsOrb ? -1 : note.IsDash ? (LineCount - 1) / 2f : LineOf(note.Target);

    /// <summary>Where a line starts, up and to the left of its landing.</summary>
    public static Vector2 Top(in Chord chord, int line) => Landing(chord, line) - Down * LineLength;

    /// <summary>The box every line of a chord covers.</summary>
    public static (Vector2 Min, Vector2 Max) Bounds(in Chord chord)
    {
        Vector2 top = Top(chord, 0);
        Vector2 end = Landing(chord, LineCount - 1);
        return (Vector2.Min(top, end), Vector2.Max(top, end));
    }

    /// <summary>Every chord in the store, by time.</summary>
    public static List<Chord> All(EntityStore store, List<Chord> into)
    {
        into.Clear();
        foreach (var (chords, _) in store.Query<Chord>().Chunks)
        {
            foreach (Chord chord in chords.Span)
                into.Add(chord);
        }

        into.Sort(static (a, b) => a.TimeMs.CompareTo(b.TimeMs));
        return into;
    }

    [ThreadStatic] private static List<Chord>? _scratch;

    /// <summary>The chord in charge at a time: the last one to start at or before it.</summary>
    public static Chord? At(EntityStore store, long timeMs)
    {
        Chord? found = null;
        foreach (Chord chord in All(store, _scratch ??= []))
        {
            if (chord.TimeMs > timeMs)
                break;
            found = chord;
        }

        return found;
    }

    /// <summary>
    /// How long a chord shows: from one approach before its time until the
    /// next chord's time (open-ended for the last one).
    /// </summary>
    public static (long From, long To) Shown(in Chord chord, List<Chord> sorted)
    {
        long to = long.MaxValue;
        foreach (Chord other in sorted)
        {
            if (other.TimeMs > chord.TimeMs)
            {
                to = other.TimeMs;
                break;
            }
        }

        return (chord.TimeMs - ApproachMs, to);
    }

    /// <summary>Where a note meets the judgement line: its key's line in its chord, or its own position without one.</summary>
    public static Vector2 Landing(EntityStore store, in Note note) =>
        At(store, note.TimeMs) is { } chord && LaneOf(note) is >= 0 and var lane
            ? Landing(chord, lane)
            : note.Position;

    /// <summary>
    /// Where a note is at a time: sliding down its line toward the judgement
    /// line, resting there from its time on. Without a chord, its own position.
    /// </summary>
    public static Vector2 NoteAt(EntityStore store, in Note note, double nowMs)
    {
        if (At(store, note.TimeMs) is not { } chord || LaneOf(note) is not (>= 0 and var lane))
            return note.Position;

        float left = (float)Math.Clamp((note.TimeMs - nowMs) / ApproachMs, 0d, 1d);
        return Landing(chord, lane) - Down * (LineLength * left);
    }

    /// <summary>A note's opacity: in as it enters its line, full on its time, out over <see cref="LingerMs"/>.</summary>
    public static float NoteAlpha(in Note note, double nowMs)
    {
        double delta = note.TimeMs - nowMs;
        if (delta > ApproachMs || delta < -LingerMs)
            return 0f;
        if (delta >= 0)
            return (float)Math.Clamp((ApproachMs - delta) / 120d, 0d, 1d);
        return (float)(1d + delta / LingerMs);
    }
}

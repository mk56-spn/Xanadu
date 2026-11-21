using System;
using Xanadu.Model.Levels;

namespace Xanadu.Model.Playfields;

/// <summary>
/// The rework's fixed rules (started 2026-11-17): one fixed floor, and a
/// hovering blue body, a rect 1 wide by 2.5 tall, that follows the curve notes it hits.
/// </summary>
public static class PlayRules
{
    /// <summary>The fixed floor's depth, playfield units up from the field's bottom edge.</summary>
    public const float FloorDepth = 300f;

    /// <summary>Where the floor's top is, in playfield units.</summary>
    public static float FloorY(Ecs.Components.Playfield playfield) => playfield.Height - FloorDepth;

    /// <summary>The body's width, playfield units.</summary>
    public const float BodyWidth = 72f;

    /// <summary>The body's height: 2.5 times its width.</summary>
    public const float BodyHeight = BodyWidth * 2.5f;

    /// <summary>The body's size, playfield units: a rect 1 wide by 2.5 tall.</summary>
    public static Vector2 BodySize => new(BodyWidth, BodyHeight);

    /// <summary>
    /// Where the body spawns: up in the air, halfway between the field's top
    /// and the floor, two body widths in from the left edge.
    /// </summary>
    public static Vector2 SpawnPosition(Ecs.Components.Playfield playfield) =>
        new(BodyWidth * 2f, FloorY(playfield) / 2f);

    /// <summary>How much of the turn from straight up to its heading the body leans by.</summary>
    public const float TiltShare = 0.18f;

    /// <summary>The most the body leans either way, radians (20°).</summary>
    public const float MaxTilt = 0.35f;

    /// <summary>Below this speed the lean fades out, so a near-still body stands upright instead of twitching, units a second.</summary>
    public const float TiltFullSpeed = 150f;

    /// <summary>
    /// How far the body leans toward where it is heading, radians, clockwise on
    /// screen: upright going straight up, leaning right going right, left going left.
    /// </summary>
    public static float Tilt(Vector2 velocity)
    {
        float speed = velocity.Length();
        if (speed <= 0f)
            return 0f;
        float fromUp = MathF.Atan2(velocity.X, -velocity.Y);
        float lean = Math.Clamp(fromUp * TiltShare, -MaxTilt, MaxTilt);
        return lean * MathF.Min(1f, speed / TiltFullSpeed);
    }

    /// <summary>How far the body bobs up and down while hovering, playfield units: the slightest drift.</summary>
    public const float BobHeight = 7f;

    /// <summary>One bob, up and back down, ms.</summary>
    public const double BobPeriodMs = 1800d;

    /// <summary>How far down the hover bob has the body at a song time, playfield units (negative is up); looks only, the body's place is untouched.</summary>
    public static float Bob(double songMs) =>
        BobHeight * MathF.Sin((float)(songMs / BobPeriodMs * Math.Tau));

    /// <summary>How often the body puffs thrust out of its bottom, ms.</summary>
    public const double ThrustEveryMs = 33d;

    /// <summary>How long the body takes to fade in, ending at the first note, ms.</summary>
    public const double BodyFadeInMs = 1000d;

    /// <summary>
    /// How visible the body is at a song time: unseen until a second before the
    /// first note, then fading in to fully there at the note. A chart with no
    /// notes shows it throughout.
    /// </summary>
    public static float BodyAlpha(double songMs, long? firstNoteMs)
    {
        if (firstNoteMs is not long first)
            return 1f;
        return (float)Math.Clamp(1d - (first - songMs) / BodyFadeInMs, 0d, 1d);
    }

    /// <summary>Roughly how long the body takes to settle on a curve's exit speed, ms; the real time is the nearest beat division to it.</summary>
    public const double ExitSettleTargetMs = 150d;

    /// <summary>The beat divisions the exit settle may take (the editor's snap divisions).</summary>
    private static readonly int[] SettleDivisions = { 1, 2, 3, 4, 6, 8, 12 };

    /// <summary>
    /// How long the body takes to settle on a curve's exit speed after the curve
    /// ends at <paramref name="endMs"/>: the beat division there closest to
    /// <see cref="ExitSettleTargetMs"/>, so the settle stays in step with the song.
    /// </summary>
    public static double ExitSettleMs(TempoMap tempo, double endMs)
    {
        double beat = TempoMap.BeatMs(tempo.At(endMs).Bpm);
        double best = beat;
        foreach (int division in SettleDivisions)
        {
            double length = beat / division;
            if (Math.Abs(length - ExitSettleTargetMs) < Math.Abs(best - ExitSettleTargetMs))
                best = length;
        }

        return best;
    }

    /// <summary>The body's cruise speed, playfield units a second.</summary>
    public const float CruiseSpeed = 900f;

    /// <summary>The body's motion at spawn: heading right at <see cref="CruiseSpeed"/>.</summary>
    public static Vector2 SpawnVelocity => new(CruiseSpeed, 0f);
}

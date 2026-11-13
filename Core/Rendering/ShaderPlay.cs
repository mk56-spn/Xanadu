using System;
using System.Diagnostics;
using System.Numerics;

namespace Xanadu.Rendering;

/// <summary>
/// Something that happens during play that shaders can time from
/// (<c>events.</c> in <c>xanadu.play</c>, <c>clock.since(events.hit)</c>).
/// Numbered as the library's <c>Event</c>; only ever add, at the end.
/// </summary>
public enum ShaderEvent
{
    /// <summary>Any note hit, whatever its judgement (not a miss).</summary>
    Hit,
    Miss,
    FlawlessPlus,
    Flawless,
    Clean,
    Fair,
    Deficient,
    Terrible,

    /// <summary>Any judgement at all, misses and failed barriers included: the last judgement's clock.</summary>
    Judged,
    ComboBroken,
    Dash,
    Momentum,
    SongStart,
    CountdownDone,
    Death,
    BarrierPaid,
    BarrierFailed,
}

/// <summary>
/// The play state as shaders see it (<c>xanadu.play</c>): the player, the
/// score, the song and Contact's bars as plain values, and when each
/// <see cref="ShaderEvent"/> last happened. Each value has one writer: in the
/// game, the system that owns it (the session, scoring, movement…) or
/// <c>ShaderPlaySystem</c> for what is read off the ECS each frame; in the
/// tools, the shader editor's stand-ins. The renderer hands every shader a
/// copy once a frame, like <see cref="ShaderKeys"/>.
/// </summary>
public static class ShaderPlay
{
    public const int EventCount = 17;

    /// <summary>Event slots the frame carries: room to add more without changing its size.</summary>
    public const int EventSlots = 20;

    /// <summary>The player's centre on the target, in pixels.</summary>
    public static Vector2 Position;

    /// <summary>The player's velocity on the target, in pixels a second.</summary>
    public static Vector2 Velocity;

    /// <summary>The playfield on the target: its top-left corner and size, in pixels.</summary>
    public static Vector2 PlayfieldCorner, PlayfieldSize = Vector2.One;

    /// <summary>Target pixels per playfield unit, for the chart's positions (<see cref="ShaderChart"/>).</summary>
    public static float PixelsPerUnit = 1;

    public static bool Grounded, Dashing, Dead;

    /// <summary>The ground mode is HOLD (F8); false is SKID.</summary>
    public static bool GroundHold;

    /// <summary>The way the last dash or momentum note sent the player, unit length; zero before any.</summary>
    public static Vector2 LastMove;

    /// <summary>Accuracy 0..1, the combo, the best combo and the score (0 to 1,000,000).</summary>
    public static float Accuracy = 1, Combo, MaxCombo, Score;

    /// <summary>The last judgement, numbered as the game's <c>Judgement</c> (0 none, 1 FLAWLESS+ … 7 MISS, 9 CONTACT).</summary>
    public static float LastJudgement;

    /// <summary>How far through the song, 0..1.</summary>
    public static float SongProgress;

    /// <summary>Song beats from the song's timing, the song's seconds, its tempo here.</summary>
    public static float Beat, SongSeconds, Bpm = 120;

    /// <summary>Seconds until the next note is due; <see cref="ShaderKeys.Never"/> when none is left.</summary>
    public static float UntilNote = ShaderKeys.Never;

    /// <summary>Contact's three bars, 0 to <see cref="ContactCap"/> each.</summary>
    public static Vector3 ContactBars;

    public const float ContactCap = 5;

    private static readonly long?[] HappenedAt = new long?[EventCount];

    /// <summary>The event happened now.</summary>
    public static void Mark(ShaderEvent happened)
    {
        int index = (int)happened;
        if (index >= 0 && index < EventCount)
            HappenedAt[index] = Stopwatch.GetTimestamp();
    }

    /// <summary>Seconds since the event last happened, or <see cref="ShaderKeys.Never"/>.</summary>
    public static float Since(ShaderEvent happened)
    {
        int index = (int)happened;
        return index >= 0 && index < EventCount && HappenedAt[index] is { } at
            ? (float)Math.Min(ShaderKeys.Never, Stopwatch.GetElapsedTime(at).TotalSeconds)
            : ShaderKeys.Never;
    }

    /// <summary>The event slot <paramref name="slot"/> of the frame: seconds since, or never for a spare slot.</summary>
    public static float SinceSlot(int slot) => slot < EventCount ? Since((ShaderEvent)slot) : ShaderKeys.Never;

    /// <summary>Back to how a fresh run starts: nothing has happened and every value is at rest.</summary>
    public static void Clear()
    {
        Array.Clear(HappenedAt);
        Position = Velocity = LastMove = PlayfieldCorner = Vector2.Zero;
        PlayfieldSize = Vector2.One;
        PixelsPerUnit = 1;
        Grounded = Dashing = Dead = GroundHold = false;
        Accuracy = 1;
        Combo = MaxCombo = Score = LastJudgement = SongProgress = Beat = SongSeconds = 0;
        Bpm = 120;
        UntilNote = ShaderKeys.Never;
        ContactBars = Vector3.Zero;
    }
}

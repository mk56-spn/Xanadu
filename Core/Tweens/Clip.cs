using System;
using System.Runtime.InteropServices;
using Xanadu.Core.Maths;
using Xanadu.Core.Rendering;
using Xanadu.Core.Timeline;

namespace Xanadu.Core.Tweens;

/// <summary>
/// Where a clip's time comes from: the clocks the shader library's <c>clock</c>
/// has, under the same names, so a clip means the same on both sides.
/// </summary>
public enum ClipClock : byte
{
    /// <summary>Seconds since the app started: the shader's <c>clock</c> (<c>time</c>).</summary>
    Seconds,

    /// <summary>Music beats since the music started: the shader's <c>clock.beats</c> (<c>beat</c>).</summary>
    Beats,
}

/// <summary>A clip's copies, as the shader's <c>Clip</c> builders make them.</summary>
[Flags]
public enum ClipShape : byte
{
    None = 0,

    /// <summary><c>bounced()</c>: forward then back, taking twice as long.</summary>
    Bounced = 1,

    /// <summary><c>reversed()</c>: 1 to 0.</summary>
    Reversed = 2,
}

/// <summary>
/// Where each <see cref="ClipClock"/> is now on the CPU. Seconds: the host
/// writes it once a frame, from the same start as the shader's <c>time</c>.
/// Beats: the shader's own <c>beat</c> (<see cref="ShaderClock.Beat"/>), whoever
/// plays music writes it. One writer each, the same numbers the GPU reads.
/// </summary>
public static class ClipClocks
{
    private static float _seconds;

    public static float Seconds => _seconds;

    public static float Beats => ShaderClock.Beat;

    public static float At(ClipClock clock) => clock == ClipClock.Beats ? Beats : _seconds;

    /// <summary>The host's once-a-frame write; nothing else writes it.</summary>
    public static void SetSeconds(float seconds) => _seconds = seconds;
}

/// <summary>
/// A span of time on a clock: the shader library's <c>Clip</c> (xanadu.anim),
/// in C#. Same builders (<see cref="Eased"/>, <see cref="Delayed"/>,
/// <see cref="Staggered"/>, <see cref="Bounced"/>, <see cref="Reversed"/>,
/// <see cref="Then"/>, <see cref="Part"/>), same readings (<see cref="LinearAt"/>,
/// <see cref="At"/>, <see cref="Progress"/>, <see cref="Mixed(float, float)"/>),
/// same maths line for line: a clip here and its twin in a shader land on the
/// same progress at the same reading of the same clock. Change both or neither.
/// <para>
/// Easings are <see cref="EasePreset"/>s; xanadu/tween.slang maps them onto
/// the shader's <c>eases.</c>, which are the same curves (<see cref="EaseCurves"/>).
/// 12 bytes, as tweens carry it to the GPU.
/// </para>
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct Clip(ClipClock clock, float start, float length)
{
    /// <summary>Where it starts on its clock, in seconds or beats.</summary>
    public float Start = start;

    /// <summary>How long one way takes, in seconds or beats.</summary>
    public float Length = length;

    private byte _ease = (byte)EasePreset.Linear;
    public ClipClock Clock = clock;
    public ClipShape Shape;
    private byte _pad;

    /// <summary><c>clock.clip(start, length)</c>.</summary>
    public static Clip Seconds(float start, float length) => new(ClipClock.Seconds, start, length);

    /// <summary><c>clock.beats.clip(start, length)</c>.</summary>
    public static Clip Beats(float start, float length) => new(ClipClock.Beats, start, length);

    /// <summary>A clip starting now on the seconds clock: what UI reactions play on.</summary>
    public static Clip FromNow(float length) => Seconds(ClipClocks.Seconds, length);

    public EasePreset Ease
    {
        readonly get => (EasePreset)_ease;
        set => _ease = (byte)value;
    }

    /// <summary>A copy with an easing.</summary>
    public readonly Clip Eased(EasePreset ease) => this with { Ease = ease };

    /// <summary>A copy that starts <paramref name="by"/> later.</summary>
    public readonly Clip Delayed(float by) => this with { Start = Start + by };

    /// <summary>A copy that starts <paramref name="index"/> × <paramref name="step"/> later: per row, per tile.</summary>
    public readonly Clip Staggered(float index, float step) => Delayed(index * step);

    /// <summary>A copy that plays forward then back, taking twice as long.</summary>
    public readonly Clip Bounced() => this with { Shape = Shape | ClipShape.Bounced };

    /// <summary>A copy that plays backwards: 1 to 0.</summary>
    public readonly Clip Reversed() => this with { Shape = Shape ^ ClipShape.Reversed };

    private readonly bool Bounces => (Shape & ClipShape.Bounced) != 0;

    private readonly bool Backwards => (Shape & ClipShape.Reversed) != 0;

    /// <summary>How long it takes from start to end (twice its length when bounced).</summary>
    public readonly float Span => Bounces ? Length * 2f : Length;

    /// <summary>Where it ends on its clock.</summary>
    public readonly float End => Start + Span;

    /// <summary>A new clip, <paramref name="length"/> long, starting where this one ends.</summary>
    public readonly Clip Then(float length) => new(Clock, End, length);

    /// <summary>A new, linear clip over the stretch from <paramref name="from"/> to <paramref name="to"/> (fractions of its span).</summary>
    public readonly Clip Part(float from, float to) => new(Clock, Start + from * Span, (to - from) * Span);

    /// <summary>Progress without the easing at a reading of its clock, 0..1.</summary>
    public readonly float LinearAt(float now)
    {
        float p = (now - Start) / Math.Max(Length, 1e-5f);
        p = Bounces ? 1f - Math.Abs(1f - Math.Clamp(p, 0f, 2f)) : Math.Clamp(p, 0f, 1f);
        return Backwards ? 1f - p : p;
    }

    /// <summary>Eased progress at a reading of its clock (overshooting for back and elastic).</summary>
    public readonly float At(float now) => EasePresets.Progress(Ease, LinearAt(now));

    /// <summary>Its clock's reading now.</summary>
    public readonly float Now => ClipClocks.At(Clock);

    public readonly float Linear => LinearAt(Now);

    /// <summary>Eased progress now: 0 before it starts, 1 after it ends.</summary>
    public readonly float Progress => At(Now);

    public readonly bool Started => Now >= Start;

    public readonly bool Finished => Now >= End;

    public readonly bool Playing => Started && !Finished;

    public readonly float Mixed(float from, float to) => from + (to - from) * Progress;

    public readonly Vector2 Mixed(Vector2 from, Vector2 to) => from + (to - from) * Progress;

    public readonly Color Mixed(Color from, Color to) => from.Lerp(to, Progress);
}

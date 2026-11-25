namespace Xanadu.Model.Playfields;

/// <summary>
/// The stretch of song time a pad or a dash ender is in the level, so a long
/// level isn't cluttered with every one at once: drawn and doing anything only
/// from its <c>FromMs</c> to its <c>ToMs</c>, fading in and out at the ends.
/// Placed ones get <see cref="Around"/> the time they were placed at.
/// </summary>
public static class SongSpans
{
    /// <summary>How long before its time an item comes in by default, ms.</summary>
    public const long LeadMs = 1200;

    /// <summary>How long after its time an item stays by default, ms.</summary>
    public const long TrailMs = 400;

    /// <summary>How long an item takes to fade in or out at the ends of its span, ms.</summary>
    private const double FadeMs = 200;

    /// <summary>The default span for an item placed at, or used at, <paramref name="timeMs"/>.</summary>
    public static (long FromMs, long ToMs) Around(long timeMs) => (timeMs - LeadMs, timeMs + TrailMs);

    /// <summary>Whether song time <paramref name="ms"/> is in the span.</summary>
    public static bool Holds(long fromMs, long toMs, double ms) => ms >= fromMs && ms <= toMs;

    /// <summary>How shown an item is at <paramref name="ms"/>: 0 outside its span, easing to 1 inside it.</summary>
    public static float Alpha(long fromMs, long toMs, double ms) =>
        !Holds(fromMs, toMs, ms) ? 0f : (float)Math.Clamp(Math.Min(ms - (double)fromMs, toMs - ms) / FadeMs, 0d, 1d);
}

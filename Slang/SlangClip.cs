using System.Collections.Generic;

namespace Xanadu.Slang;

/// <summary>
/// A clip found in shader code (<c>xanadu.anim</c>), worked out from its text
/// alone: where it sits on its clock, how it eases, and the keys that follow it.
/// Times are in the clock's own units (seconds, or beats when <see cref="Beats"/>).
/// </summary>
/// <param name="Name">The local it is declared as.</param>
/// <param name="Line">0-based line of its declaration.</param>
/// <param name="Column">0-based column of its name there.</param>
/// <param name="Loop">How long its clock's loop is; 0 when the clock doesn't loop.</param>
/// <param name="Start">Where it starts on its clock; NaN when the code doesn't fix it.</param>
/// <param name="Length">How long one pass is; NaN when the code doesn't fix it.</param>
/// <param name="Easing">Its easing, numbered as <c>ease.by</c>.</param>
/// <param name="StaggerStep">Each copy's delay when it is staggered by a value only the pixel knows; 0 when it isn't.</param>
/// <param name="Keys">The progress (0..1) of each key of every <c>Keys</c> read at it.</param>
/// <param name="Pressed">What its clock counts from: a play key (<c>clock.since(keys.left)</c>), numbered as <see cref="SlangClips.PlayKeys"/>; a play event (<c>clock.since(events.hit)</c>), <see cref="SlangClips.FirstEvent"/> plus its id in <see cref="SlangClips.Events"/>; -1 for the song's time.</param>
public sealed record SlangClip(
    string Name,
    int Line,
    int Column,
    bool Beats,
    double Loop,
    double Start,
    double Length,
    int Easing,
    bool Bounced,
    bool Reversed,
    double StaggerStep,
    IReadOnlyList<double> Keys,
    int Pressed = -1)
{
    /// <summary>Whether it plays from a key's press or an event rather than on the song's time.</summary>
    public bool OnKey => Pressed >= 0;

    /// <summary>Whether it plays from a play event (<c>events.</c>) rather than a key.</summary>
    public bool OnEvent => Pressed >= SlangClips.FirstEvent;

    /// <summary>Whether the code fixes where it is, so it can be drawn.</summary>
    public bool Fixed => double.IsFinite(Start) && double.IsFinite(Length);

    /// <summary>Start to end: twice its length when bounced.</summary>
    public double Span => Bounced ? Length * 2 : Length;

    public double End => Start + Span;

    /// <summary>Eased progress at a fraction of its <see cref="Span"/>, as the library works it out.</summary>
    public float ProgressAt(float fraction)
    {
        float p = Bounced ? 1f - MathF.Abs(1f - Math.Clamp(fraction * 2f, 0f, 2f)) : Math.Clamp(fraction, 0f, 1f);
        return SlangClips.Ease(Easing, Reversed ? 1f - p : p);
    }

    /// <summary>The fraction of one pass at which its eased progress first reaches <paramref name="key"/>.</summary>
    public double KeyFraction(double key)
    {
        const int Steps = 200;
        float previous = SlangClips.Ease(Easing, Reversed ? 1 : 0);
        for (int i = 1; i <= Steps; i++)
        {
            float t = (float)i / Steps;
            float now = SlangClips.Ease(Easing, Reversed ? 1 - t : t);
            if ((previous - key) * (now - key) <= 0)
                return t;
            previous = now;
        }

        return 1;
    }
}

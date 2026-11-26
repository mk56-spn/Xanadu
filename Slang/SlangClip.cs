using System.Collections.Generic;

namespace Xanadu.Slang;

/// <summary>
/// A clip of shader code (<c>xanadu.anim</c>), as it reported itself when the probe ran the code
/// (<see cref="ClipProbes"/>): where it sits on its clock, how it eases, and the keys read at it.
/// Times are in the clock's own units (seconds, or beats when <see cref="Beats"/>).
/// </summary>
/// <param name="Name">The local it is declared as (its site), or "clip n" for one read without a name.</param>
/// <param name="Line">0-based line of its declaration; -1 without one.</param>
/// <param name="Column">0-based column of its name there.</param>
/// <param name="Loop">How long its clock's loop is; 0 when the clock doesn't loop.</param>
/// <param name="Start">Where it starts on its clock (its first copy's, when staggered).</param>
/// <param name="Length">How long one pass is.</param>
/// <param name="Easing">Its easing, numbered as <c>ease.by</c>.</param>
/// <param name="StaggerStep">The step between its copies' starts when the probe saw it start at several times (staggered by
/// copy or by a loop); 0 when it didn't.</param>
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

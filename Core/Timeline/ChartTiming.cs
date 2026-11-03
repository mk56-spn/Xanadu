namespace Xanadu.Timeline;

/// <summary>How chart times relate to the sound of the song.</summary>
public static class ChartTiming
{
    /// <summary>
    /// How long after a note's time its sound is, in milliseconds. Rhythm-game charts
    /// have latency baked into them: on Dear Brave's osu! map the audio lands about
    /// 25 ms after the hit times, and osu!'s editor documents the figure as 20
    /// (<c>Editor.WAVEFORM_VISUAL_OFFSET</c>), checked against published maps to within
    /// 2 ms. The editor's waveform and the offset wizard both allow for it, so a note
    /// placed on a hit in the waveform is one a calibrated player taps dead on.
    /// </summary>
    public const double AudioLagMs = 20d;
}

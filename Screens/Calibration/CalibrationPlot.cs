using Xanadu.Model.Scoring;

namespace Xanadu.Screens.Calibration;

/// <summary>The scale of the calibration plot, shared by the scene that builds it and the system that marks taps on it.</summary>
public static class CalibrationPlot
{
    /// <summary>Pixels per millisecond of the plot, so the Flawless window is wide enough to read.</summary>
    public const float PixelsPerMs = 3f;

    /// <summary>The plot runs this far either side of on time: the miss window.</summary>
    public static float HalfRangeMs => (float)ScoreRules.JudgementDeviation(Judgement.Miss);
}

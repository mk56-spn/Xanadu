using System;

namespace Xanadu.Scoring;

/// <summary>Timing judgement tiers for a play note.</summary>
public enum Judgement
{
    None,
    FlawlessP,
    Flawless,
    Clean,
    Fair,
    Deficient,
    Terrible,
    Miss,
    Mine,
    Contact,
}

/// <summary>Judgement windows, accuracy weights, and rank thresholds.</summary>
public static class ScoreRules
{
    public const int MaxScore = 1_000_000;

    public const double MineAccuracyPenalty = 0.01;

    /// <summary>Accuracy lost when a contact barrier is not fully paid.</summary>
    public const double ContactAccuracyPenalty = 0.02;

    /// <summary>Accuracy lost when a rail hold is released early.</summary>
    public const double HoldAccuracyPenalty = 0.01;

    /// <summary>The max deviation a given judgement type can have.</summary>
    public static double JudgementDeviation(Judgement judgement)
    {
        return judgement switch
        {
            Judgement.FlawlessP => 8.3,
            Judgement.Flawless => 16,
            Judgement.Clean => 27,
            Judgement.Fair => 40,
            Judgement.Deficient => 55,
            Judgement.Terrible => 80,
            Judgement.Miss => 150,
            _ => throw new ArgumentOutOfRangeException(
                nameof(judgement),
                judgement,
                null),
        };
    }

    /// <summary>Per-note accuracy weight for a judgement.</summary>
    public static double AccuracyFor(Judgement judgement)
    {
        return judgement switch
        {
            Judgement.FlawlessP => 1.0,
            Judgement.Flawless => 0.99,
            Judgement.Clean => 0.98,
            Judgement.Fair => 0.95,
            Judgement.Deficient => 0.93,
            Judgement.Terrible => 0.90,
            Judgement.Miss => 0.0,
            Judgement.Mine => 0.0,
            _ => 0.0,
        };
    }

    public static Judgement Judge(long offsetMs)
    {
        double error = Math.Abs(offsetMs);

        if (error <= JudgementDeviation(Judgement.FlawlessP))
            return Judgement.FlawlessP;
        if (error <= JudgementDeviation(Judgement.Flawless))
            return Judgement.Flawless;
        if (error <= JudgementDeviation(Judgement.Clean))
            return Judgement.Clean;
        if (error <= JudgementDeviation(Judgement.Fair))
            return Judgement.Fair;
        if (error <= JudgementDeviation(Judgement.Deficient))
            return Judgement.Deficient;
        if (error <= JudgementDeviation(Judgement.Terrible))
            return Judgement.Terrible;
        return Judgement.Miss;
    }

    public static string RankFor(double accuracy)
    {
        if (accuracy >= 0.999) return "SSS";
        if (accuracy >= 0.99) return "SS";
        if (accuracy >= 0.98) return "S";
        if (accuracy >= 0.95) return "A";
        if (accuracy >= 0.93) return "B";
        if (accuracy >= 0.90) return "C";
        return "FAIL";
    }

    public static string LabelFor(Judgement judgement) => judgement switch
    {
        Judgement.FlawlessP => "FLAWLESS+",
        Judgement.Flawless => "FLAWLESS",
        Judgement.Clean => "CLEAN",
        Judgement.Fair => "FAIR",
        Judgement.Deficient => "DEFICIENT",
        Judgement.Terrible => "TERRIBLE",
        Judgement.Miss => "MISS",
        Judgement.Mine => "BOOM",
        Judgement.Contact => "CONTACT",
        _ => "",
    };
}

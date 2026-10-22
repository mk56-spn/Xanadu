using System;
using Writer.Ecs;
using Writer.Scoring;

namespace Writer.Systems.Playfields;

/// <summary>Shared judgement application for notes and movement notes.</summary>
public static class PlayScoring
{
    public static Judgement Apply(ref ScoreState score, long offsetMs)
    {
        Judgement judgement = ScoreRules.Judge(offsetMs);

        if (judgement != Judgement.Miss)
            score.NotesHit++;

        Count(ref score, judgement);
        score.HasJudgement = true;
        score.LastJudgement = judgement;
        score.LastOffsetMs = offsetMs;
        score.AccuracyPoints += ScoreRules.AccuracyFor(judgement);

        if (judgement == Judgement.Miss)
        {
            score.Combo = 0;
        }
        else
        {
            score.Combo++;
            score.MaxCombo = Math.Max(score.MaxCombo, score.Combo);
            if (score.TotalNotes > 0)
            {
                score.ScorePoints = Math.Min(
                    1.0,
                    score.ScorePoints + 1.0 / score.TotalNotes);
            }
        }

        return judgement;
    }

    public static void Count(ref ScoreState score, Judgement judgement)
    {
        switch (judgement)
        {
            case Judgement.FlawlessP:
                score.FlawlessPCount++;
                break;
            case Judgement.Flawless:
                score.FlawlessCount++;
                break;
            case Judgement.Clean:
                score.CleanCount++;
                break;
            case Judgement.Fair:
                score.FairCount++;
                break;
            case Judgement.Deficient:
                score.DeficientCount++;
                break;
            case Judgement.Terrible:
                score.TerribleCount++;
                break;
            case Judgement.Miss:
                score.MissCount++;
                break;
        }
    }
}

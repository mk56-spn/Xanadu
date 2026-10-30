using System;
using Godot;
using Xanadu.Ecs;
using Xanadu.Scoring;

namespace Xanadu.Systems.Playfields;

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

    /// <summary>
    /// A contact barrier that could not be fully paid. It is a check rather
    /// than a timed note, so it applies accuracy loss and breaks combo without
    /// counting as a missed note.
    /// </summary>
    public static void FailContact(
        ref ScoreState score,
        Vector2 position,
        long timeMs)
    {
        score.AccuracyPenalty += ScoreRules.ContactAccuracyPenalty * score.TotalNotes;
        score.Combo = 0;
        score.HasJudgement = true;
        score.LastJudgement = Judgement.Contact;
        score.JudgementPosition = position;
        score.JudgementTimeMs = timeMs;
    }

    /// <summary>A rail hold released before its end.</summary>
    public static void FailHold(
        ref ScoreState score,
        Vector2 position,
        long timeMs)
    {
        score.AccuracyPenalty += ScoreRules.HoldAccuracyPenalty * score.TotalNotes;
        score.Combo = 0;
        score.HasJudgement = true;
        score.LastJudgement = Judgement.Miss;
        score.JudgementPosition = position;
        score.JudgementTimeMs = timeMs;
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

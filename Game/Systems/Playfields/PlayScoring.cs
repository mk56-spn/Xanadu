using System;
using Xanadu.Ecs;
using Xanadu.Rendering;
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

        Mark(judgement, comboBroken: judgement == Judgement.Miss && score.Combo > 0);
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
        Mark(Judgement.Contact, comboBroken: score.Combo > 0);
        score.Combo = 0;
        score.HasJudgement = true;
        score.LastJudgement = Judgement.Contact;
        score.JudgementPosition = position;
        score.JudgementTimeMs = timeMs;
    }

    /// <summary>Level shaders' clocks for a judgement (<c>events.</c> in xanadu.play).</summary>
    internal static void Mark(Judgement judgement, bool comboBroken)
    {
        ShaderPlay.Mark(ShaderEvent.Judged);
        if (comboBroken)
            ShaderPlay.Mark(ShaderEvent.ComboBroken);
        ShaderEvent? tier = judgement switch
        {
            Judgement.FlawlessP => ShaderEvent.FlawlessPlus,
            Judgement.Flawless => ShaderEvent.Flawless,
            Judgement.Clean => ShaderEvent.Clean,
            Judgement.Fair => ShaderEvent.Fair,
            Judgement.Deficient => ShaderEvent.Deficient,
            Judgement.Terrible => ShaderEvent.Terrible,
            Judgement.Miss => ShaderEvent.Miss,
            Judgement.Contact => ShaderEvent.BarrierFailed,
            _ => null,
        };
        if (tier is { } marked)
            ShaderPlay.Mark(marked);
        if (judgement is not (Judgement.Miss or Judgement.Contact or Judgement.Mine))
            ShaderPlay.Mark(ShaderEvent.Hit);
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

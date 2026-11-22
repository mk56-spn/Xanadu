using System;
using Friflo.Engine.ECS;
using Xanadu.Model.Scoring;

namespace Xanadu.Model.Ecs.Components;

/// <summary>Play-mode score, accuracy, combo, and display entities.</summary>
public struct ScoreState(
    Entity scoreText,
    Entity comboText,
    Entity accuracyText,
    Entity judgementText,
    int totalNotes) : IComponent
{
    public Entity ScoreText = scoreText;
    public Entity ComboText = comboText;
    public Entity AccuracyText = accuracyText;
    public Entity JudgementText = judgementText;
    public int TotalNotes = totalNotes;
    public int NotesHit;
    public int Combo;
    public int MaxCombo;
    public double ScorePoints;
    public double AccuracyPoints;
    public double AccuracyPenalty;
    public Judgement LastJudgement;
    public bool HasJudgement;
    public long LastOffsetMs;

    /// <summary>Screen position of the note that was judged; judgements sit on it.</summary>
    public Vector2 JudgementPosition;
    /// <summary>When the last judgement popped, on the shown (smooth) clock, ms; the pop ages by that clock.</summary>
    public double JudgementTimeMs;
    public int FlawlessPCount;
    public int FlawlessCount;
    public int CleanCount;
    public int FairCount;
    public int DeficientCount;
    public int TerribleCount;
    public int MissCount;

    public readonly double Accuracy =>
        TotalNotes > 0
            ? Math.Clamp(
                (AccuracyPoints - AccuracyPenalty) / TotalNotes,
                0,
                1)
            : 0;

    public readonly long Score => Math.Clamp(
        (long)Math.Round(ScorePoints * ScoreRules.MaxScore),
        0,
        ScoreRules.MaxScore);
}

/// <summary>Geometry and marker for the play-mode timing error bar.</summary>
public struct ErrorBar(
    Entity marker,
    float centerX,
    float y,
    float halfWidth,
    float maxOffsetMs = 150f) : IComponent
{
    public Entity Marker = marker;
    public float CenterX = centerX;
    public float BaseCenterX = centerX;
    public float Y = y;
    public float BaseY = y;
    public float HalfWidth = halfWidth;
    public float MaxOffsetMs = maxOffsetMs;
}

/// <summary>The EARLY / LATE label shown under the judgement on the hit note.</summary>
public struct TimingIndicator(Entity text) : IComponent
{
    public Entity Text = text;
}

/// <summary>
/// The big combo counter: the number and the word under it, and how far
/// through its animations it is. It swells and flashes on every hit, harder
/// on every fiftieth, and flashes red when the combo breaks.
/// </summary>
public struct ComboHud(Entity number, Entity caption) : IComponent
{
    public Entity Number = number;
    public Entity Caption = caption;

    /// <summary>The combo the counter last showed; its swell and flashes are tweens on the two labels.</summary>
    public int Shown;
}

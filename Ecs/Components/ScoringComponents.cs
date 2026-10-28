using System;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Scoring;

namespace Xanadu.Ecs;

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
    public long JudgementTimeMs;
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

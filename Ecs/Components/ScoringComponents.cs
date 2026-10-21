using System;
using Friflo.Engine.ECS;
using Godot;
using Writer.Scoring;

namespace Writer.Ecs;

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
    public Judgement LastJudgement;
    public bool HasJudgement;
    public long LastOffsetMs;
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
        TotalNotes > 0 ? AccuracyPoints / TotalNotes : 0;

    public readonly long Score => Math.Clamp(
        (long)Math.Round(ScorePoints * ScoreRules.MaxScore),
        0,
        ScoreRules.MaxScore);
}

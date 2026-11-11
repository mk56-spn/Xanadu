namespace Xanadu.Scenes;

/// <summary>Snapshot of a finished play used by the results scene.</summary>
public readonly record struct PlayResults(
    string LevelName,
    long Score,
    double Accuracy,
    int MaxCombo,
    int NotesHit,
    int TotalNotes,
    int FlawlessPCount = 0,
    int FlawlessCount = 0,
    int CleanCount = 0,
    int FairCount = 0,
    int DeficientCount = 0,
    int TerribleCount = 0,
    int MissCount = 0,

    /// <summary>The run ended in death, so it failed regardless of accuracy.</summary>
    bool Failed = false);

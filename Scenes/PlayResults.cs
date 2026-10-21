namespace Writer.Scenes;

/// <summary>Snapshot of a finished play used by the results scene.</summary>
public readonly record struct PlayResults(
    string LevelName,
    long Score,
    double Accuracy,
    int MaxCombo,
    int NotesHit,
    int TotalNotes);

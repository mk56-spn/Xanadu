using System;
using System.Collections.Generic;
using Xanadu.Settings;

namespace Xanadu.Replays;

/// <summary>A key going down or up at a point in the song.</summary>
public readonly record struct ReplayEvent(int TimeMs, KeybindAction Action, bool Down);

/// <summary>The score a replay ended with, as played when it was recorded.</summary>
public readonly record struct ReplayResult(
    long Score,
    double Accuracy,
    int MaxCombo,
    int NotesHit,
    int TotalNotes,
    int FlawlessPlus,
    int Flawless,
    int Clean,
    int Fair,
    int Deficient,
    int Terrible,
    int Miss);

/// <summary>
/// One recorded play: which level, who played it, and every key press and
/// release by song time. Replaying feeds those back into the same systems
/// that read the keyboard, so judgement, scoring and movement are re-derived
/// rather than stored.
/// </summary>
public sealed record Replay(
    string LevelId,
    string LevelName,
    string SongId,
    string Player,
    DateTimeOffset RecordedAt,
    int NoteCount,
    ReplayResult Result,
    IReadOnlyList<ReplayEvent> Events);

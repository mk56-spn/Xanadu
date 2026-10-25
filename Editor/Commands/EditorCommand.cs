using Writer.Ecs;
using Writer.Settings;
using Writer.Levels;

namespace Writer.Editor.Commands;

/// <summary>Editor intents emitted by UI controls and consumed by systems.</summary>
public enum EditorCommandType
{
    SetName,
    SetArtist,
    SetDifficulty,
    SetBpm,
    SetOffset,
    SetSnap,
    SetPlacementMode,
    TogglePlayback,
    StopPlayback,
    Seek,
    SetMetronome,
    BeginPreview,
    EndPreview,
    LoadSong,
    LoadSongById,
    DeleteSelection,
    CopySelection,
    PasteSelection,
    SetTarget,
    MoveSelection,
    ZoomTimeline,
    Undo,
    Redo,
    ApplyLevel,
    SetLevelFile,
}

/// <summary>Data-only editor command.</summary>
public readonly record struct EditorCommand(
    EditorCommandType Type,
    string? Text = null,
    int IntValue = 0,
    long LongValue = 0,
    LevelDocument? Document = null)
{
    public static EditorCommand SetName(string value) =>
        new(EditorCommandType.SetName, Text: value);

    public static EditorCommand SetArtist(string value) =>
        new(EditorCommandType.SetArtist, Text: value);

    public static EditorCommand SetDifficulty(string value) =>
        new(EditorCommandType.SetDifficulty, Text: value);

    public static EditorCommand SetBpm(int value) =>
        new(EditorCommandType.SetBpm, IntValue: value);

    public static EditorCommand SetOffset(long value) =>
        new(EditorCommandType.SetOffset, LongValue: value);

    public static EditorCommand SetSnap(int value) =>
        new(EditorCommandType.SetSnap, IntValue: value);

    public static EditorCommand SetPlacementMode(EditorPlacementMode mode) =>
        new(EditorCommandType.SetPlacementMode, IntValue: (int)mode);

    public static EditorCommand TogglePlayback() =>
        new(EditorCommandType.TogglePlayback);

    public static EditorCommand StopPlayback() =>
        new(EditorCommandType.StopPlayback);

    public static EditorCommand Seek(long timeMs) =>
        new(EditorCommandType.Seek, LongValue: timeMs);

    public static EditorCommand SetMetronome(bool enabled) =>
        new(EditorCommandType.SetMetronome, IntValue: enabled ? 1 : 0);

    public static EditorCommand BeginPreview() =>
        new(EditorCommandType.BeginPreview);

    public static EditorCommand EndPreview() =>
        new(EditorCommandType.EndPreview);

    public static EditorCommand LoadSong(string path) =>
        new(EditorCommandType.LoadSong, Text: path);

    public static EditorCommand LoadSongById(string id) =>
        new(EditorCommandType.LoadSongById, Text: id);

    public static EditorCommand DeleteSelection() =>
        new(EditorCommandType.DeleteSelection);

    public static EditorCommand CopySelection() =>
        new(EditorCommandType.CopySelection);

    public static EditorCommand PasteSelection() =>
        new(EditorCommandType.PasteSelection);

    public static EditorCommand SetTarget(KeybindAction action) =>
        new(EditorCommandType.SetTarget, IntValue: (int)action);

    public static EditorCommand MoveSelection(int direction) =>
        new(EditorCommandType.MoveSelection, IntValue: direction);

    public static EditorCommand ZoomTimeline(int direction) =>
        new(EditorCommandType.ZoomTimeline, IntValue: direction);

    public static EditorCommand Undo() =>
        new(EditorCommandType.Undo);

    public static EditorCommand Redo() =>
        new(EditorCommandType.Redo);

    public static EditorCommand ApplyLevel(LevelDocument document, string path) =>
        new(EditorCommandType.ApplyLevel, Text: path, Document: document);

    public static EditorCommand SetLevelFile(string path) =>
        new(EditorCommandType.SetLevelFile, Text: path);
}

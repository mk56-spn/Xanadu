using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Levels;
using Xanadu.Model.Settings;

namespace Xanadu.Logic.Editor.Commands;

/// <summary>Editor intents emitted by UI controls and consumed by systems.</summary>
public enum EditorCommandType
{
    SetName,
    SetArtist,
    SetDifficulty,
    SetSnap,
    SetPlacementMode,
    TogglePlayback,
    StopPlayback,
    Seek,
    SetMetronome,
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
    RecoverLevel,
    SetLevelFile,
    NewGroup,
    PickGroup,
    AddToGroup,
    RemoveFromGroup,
    DeleteGroup,
    KeyGroup,
    DeleteGroupKeys,
    StepGroupLoop,
    RenameGroup,
    ToggleOneMember,
    ToggleGroupEase,
    EaseGroupKeys,
}

/// <summary>Data-only editor command.</summary>
public readonly record struct EditorCommand(
    EditorCommandType Type,
    string? Text = null,
    int IntValue = 0,
    long LongValue = 0,
    LevelDocument? Document = null,
    string? Text2 = null,
    Color? ColorValue = null,
    float FloatValue = 0f)
{
    public static EditorCommand SetName(string value) =>
        new(EditorCommandType.SetName, value);

    public static EditorCommand SetArtist(string value) =>
        new(EditorCommandType.SetArtist, value);

    public static EditorCommand SetDifficulty(string value) =>
        new(EditorCommandType.SetDifficulty, value);

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

    public static EditorCommand LoadSong(string path) =>
        new(EditorCommandType.LoadSong, path);

    public static EditorCommand LoadSongById(string id) =>
        new(EditorCommandType.LoadSongById, id);

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
        new(EditorCommandType.ApplyLevel, path, Document: document);

    /// <summary>Like <see cref="ApplyLevel" />, but the level is unsaved work, not what is on disk.</summary>
    public static EditorCommand RecoverLevel(LevelDocument document, string path) =>
        new(EditorCommandType.RecoverLevel, path, Document: document);

    public static EditorCommand SetLevelFile(string path) =>
        new(EditorCommandType.SetLevelFile, path);

    /// <summary>A new group holding the selected decos, picked straight away.</summary>
    public static EditorCommand NewGroup() => new(EditorCommandType.NewGroup);

    /// <summary>Picks the group at <paramref name="index"/> to edit and selects its members.</summary>
    public static EditorCommand PickGroup(int index) => new(EditorCommandType.PickGroup, IntValue: index);

    public static EditorCommand AddToGroup() => new(EditorCommandType.AddToGroup);

    public static EditorCommand RemoveFromGroup() => new(EditorCommandType.RemoveFromGroup);

    public static EditorCommand DeleteGroup() => new(EditorCommandType.DeleteGroup);

    /// <summary>
    /// Keys a field of the picked group (its index in <see cref="LevelItemFields.All"/>)
    /// at the playhead. While <paramref name="live"/> a drag is still going; the
    /// drag is one undo step, recorded when it ends.
    /// </summary>
    public static EditorCommand KeyGroup(int field, float value, bool live) =>
        new(EditorCommandType.KeyGroup, IntValue: field, LongValue: live ? 1 : 0, FloatValue: value);

    /// <summary>Takes out the picked group's keys at the playhead.</summary>
    public static EditorCommand DeleteGroupKeys() => new(EditorCommandType.DeleteGroupKeys);

    /// <summary>Steps the picked group's loop through its lengths (off, then 1 to 32 beats); <paramref name="direction"/> is -1 or +1.</summary>
    public static EditorCommand StepGroupLoop(int direction) => new(EditorCommandType.StepGroupLoop, IntValue: direction);

    public static EditorCommand RenameGroup(string name) => new(EditorCommandType.RenameGroup, name);

    /// <summary>Turns the one-member escape hatch on or off: keys go to the one selected member instead of the whole group.</summary>
    public static EditorCommand ToggleOneMember() => new(EditorCommandType.ToggleOneMember);

    /// <summary>Opens or closes the ease picker for the picked group's keys at the playhead.</summary>
    public static EditorCommand ToggleGroupEase(bool open) => new(EditorCommandType.ToggleGroupEase, IntValue: open ? 1 : 0);

    /// <summary>Sets an <see cref="Xanadu.Core.Timeline.EasePreset"/> on the keys at the playhead that the group keys go to.</summary>
    public static EditorCommand EaseGroupKeys(Xanadu.Core.Timeline.EasePreset preset) =>
        new(EditorCommandType.EaseGroupKeys, IntValue: (int)preset);
}

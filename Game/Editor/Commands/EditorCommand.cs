using Xanadu.Ecs;
using Xanadu.Settings;
using Xanadu.Levels;

namespace Xanadu.Editor.Commands;

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
    RotateSelection,
    FlipSelection,
    CycleOneWay,
    CycleSlope,
    MoveInLayer,
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
    ImportShader,
    RemoveShader,
    AssignShader,
    SetPlayerShader,
    SetFieldShader,
    SetBackgroundColor,
    SetOutsideColor,
    SetFrameColor,
}

/// <summary>Data-only editor command.</summary>
public readonly record struct EditorCommand(
    EditorCommandType Type,
    string? Text = null,
    int IntValue = 0,
    long LongValue = 0,
    LevelDocument? Document = null,
    string? Text2 = null,
    Color? ColorValue = null)
{
    public static EditorCommand SetName(string value) =>
        new(EditorCommandType.SetName, Text: value);

    public static EditorCommand SetArtist(string value) =>
        new(EditorCommandType.SetArtist, Text: value);

    public static EditorCommand SetDifficulty(string value) =>
        new(EditorCommandType.SetDifficulty, Text: value);

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
        new(EditorCommandType.LoadSong, Text: path);

    public static EditorCommand LoadSongById(string id) =>
        new(EditorCommandType.LoadSongById, Text: id);

    public static EditorCommand DeleteSelection() =>
        new(EditorCommandType.DeleteSelection);

    public static EditorCommand RotateSelection() =>
        new(EditorCommandType.RotateSelection);

    public static EditorCommand FlipSelection(bool horizontal) =>
        new(EditorCommandType.FlipSelection, IntValue: horizontal ? 1 : 0);

    public static EditorCommand CycleOneWay() =>
        new(EditorCommandType.CycleOneWay);

    public static EditorCommand CycleSlope() =>
        new(EditorCommandType.CycleSlope);

    /// <summary>Moves the selection one place forward (+1) or back (-1) through its layer.</summary>
    public static EditorCommand MoveInLayer(int direction) =>
        new(EditorCommandType.MoveInLayer, IntValue: direction);

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

    /// <summary>Like <see cref="ApplyLevel"/>, but the level is unsaved work, not what is on disk.</summary>
    public static EditorCommand RecoverLevel(LevelDocument document, string path) =>
        new(EditorCommandType.RecoverLevel, Text: path, Document: document);

    public static EditorCommand SetLevelFile(string path) =>
        new(EditorCommandType.SetLevelFile, Text: path);

    /// <summary>Puts shader source into a 1-based slot, replacing what was there.</summary>
    public static EditorCommand ImportShader(int slot, string name, string source) =>
        new(EditorCommandType.ImportShader, Text: name, IntValue: slot, Text2: source);

    /// <summary>Empties a slot and takes the shader off everything that used it.</summary>
    public static EditorCommand RemoveShader(int slot) =>
        new(EditorCommandType.RemoveShader, IntValue: slot);

    /// <summary>Sets the shader of the selected items; 0 takes it off.</summary>
    public static EditorCommand AssignShader(int slot) =>
        new(EditorCommandType.AssignShader, IntValue: slot);

    public static EditorCommand SetPlayerShader(int slot) =>
        new(EditorCommandType.SetPlayerShader, IntValue: slot);

    public static EditorCommand SetFieldShader(int slot) =>
        new(EditorCommandType.SetFieldShader, IntValue: slot);

    /// <summary>Sets the playfield background; null returns it to the theme default.</summary>
    public static EditorCommand SetBackgroundColor(Color? color) =>
        new(EditorCommandType.SetBackgroundColor, ColorValue: color);

    /// <summary>Sets the colour around the playfield; null returns it to the theme default.</summary>
    public static EditorCommand SetOutsideColor(Color? color) =>
        new(EditorCommandType.SetOutsideColor, ColorValue: color);

    /// <summary>Sets the playfield's frame and border line; null returns them to the theme default.</summary>
    public static EditorCommand SetFrameColor(Color? color) =>
        new(EditorCommandType.SetFrameColor, ColorValue: color);
}

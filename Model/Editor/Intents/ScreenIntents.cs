using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Model.Editor.Intents;

// Asks for editor screen work: files, dialogs, scene swaps, panels. The
// overlay hears the ones that swap scenes or write files and acts on its tick.

/// <summary>Saves the level to its file, or to a fresh unnamed one, and says so.</summary>
public readonly record struct SaveLevel : IIntent<SaveLevel>;

/// <summary>The autosave asks for the level to be written to its file without a word.</summary>
public readonly record struct SaveQuietly : IIntent<SaveQuietly>;

/// <summary>Opens the song picker.</summary>
public readonly record struct OpenSongPicker : IIntent<OpenSongPicker>;

/// <summary>Saves, then plays the level in the real play scene, from its start or the playhead.</summary>
public readonly record struct PreviewLevel(bool FromPlayhead) : IIntent<PreviewLevel>;

/// <summary>Leaves the editor, as Escape on the field does.</summary>
public readonly record struct LeaveEditor : IIntent<LeaveEditor>;

/// <summary>Shows LEVEL INFO at its home, or hides it.</summary>
public readonly record struct ToggleLevelInfo : IIntent<ToggleLevelInfo>;

/// <summary>Emitting editor intents from places that may run while no editor is up.</summary>
public static class EditorIntents
{
    /// <summary>Emits <paramref name="intent"/> on <paramref name="editor"/>; nothing when there is no editor.</summary>
    public static void EmitOnEditor<T>(this Friflo.Engine.ECS.Entity editor, T intent)
        where T : struct, IIntent<T>
    {
        if (!editor.IsNull)
            editor.EmitSignal(intent);
    }
}

/// <summary>Closes the open editor dialog; the song picker, while the level has no song, leaves instead.</summary>
public readonly record struct CloseEditorDialog : IIntent<CloseEditorDialog>;

/// <summary>Shows or hides the key bindings sheet.</summary>
public readonly record struct ToggleShortcutHelp : IIntent<ToggleShortcutHelp>;

/// <summary>Goes on from the editor, as Enter on the field does.</summary>
public readonly record struct GoForward : IIntent<GoForward>;

/// <summary>Fits the whole field in view (<c>EditorViewSystem.Listen</c>).</summary>
public readonly record struct FitView : IIntent<FitView>;

/// <summary>Runs one of the shared timeline's key commands on the editor's timeline.</summary>
public readonly record struct TimelineKey(Xanadu.Ui.Systems.Timeline.TimelineCommand Command) : IIntent<TimelineKey>;

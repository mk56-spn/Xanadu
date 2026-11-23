using Friflo.Engine.ECS;
using Xanadu.Logic.Editor;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Editor.Intents;
using Xanadu.Ui.Systems.Input;
using Xanadu.Ui.Systems.Timeline;

namespace Xanadu.Screens.Editor;

/// <summary>
/// The editor's keys: the host hands each press to the editor as <see cref="KeyPressed"/>;
/// <see cref="EditorShortcuts" /> says which intent it emits there, given what is up.
/// The intents only keys send are heard here too.
/// </summary>
public sealed partial class EditorOverlay
{
    private EditorShortcutScope ActiveScopes => EditorShortcuts.ActiveScopes(
        AnyDialogVisible(),
        IsBindingsHelpVisible(),
        IsWarningOpen(),
        !FindFocusedTextField().IsNull);

    private void ListenKeys(Entity editor)
    {
        editor.AddTag<KeyTarget>();
        editor.AddSignalHandler<KeyPressed>(signal =>
        {
            if (!_closed && EditorShortcuts.Find(signal.Event.Chord, ActiveScopes) is { } binding)
                binding.Intent.EmitOn(signal.Entity);
        });

        editor.AddSignalHandler<CloseEditorDialog>(_ =>
        {
            // The song picker can't be closed while the level has no song: Escape leaves instead.
            if (EditorDialogs.IsSongRequired(_entities) && EditorDialogs.IsOpen(_entities, EditorDialogKind.Song))
                _asked.Add(() => _onBack?.Invoke());
            else
                EditorDialogs.Dismiss(_entities);
        });
        editor.AddSignalHandler<ToggleShortcutHelp>(_ => ToggleBindingsHelp());
        editor.AddSignalHandler<GoForward>(_ => _asked.Add(() => _onForward?.Invoke()));
        editor.AddSignalHandler<TimelineKey>(signal => TimelineShortcuts.Run(signal.Entity, signal.Event.Command));

        editor.AddSignalHandler<Undo>(_ => SetStatus("Undo"));
        editor.AddSignalHandler<Redo>(_ => SetStatus("Redo"));
        editor.AddSignalHandler<PickPlacement>(signal => ShowPlacement(signal.Event.Mode));
    }

    private void ShowPlacement(EditorPlacementMode mode)
    {
        SetStatus(mode switch
        {
            EditorPlacementMode.Note => "Notes: click to place one at the playhead, on its key's chord line",
            EditorPlacementMode.Chord => "Chords: click where the first line meets the judgement line",
            EditorPlacementMode.Dash => "Dashes: click to place one at the playhead, across its chord",
            EditorPlacementMode.Entrance => "Entrance: click where the player comes in; click again to set its exit (fall, dash, orb)",
            EditorPlacementMode.Orb => "Orbs: click to place; with it selected, move on and click to add height keys",
            EditorPlacementMode.Pad => "Pads: click to place one; touching it launches the player up",
            EditorPlacementMode.TimedPad => "Timed pads: click to place one firing at the playhead",
            EditorPlacementMode.Draft => "Updrafts: click to place; still selected, click above it to set its hover",
            EditorPlacementMode.DashStop => "Dash enders: click to place; a dash entering one ends there",
            EditorPlacementMode.Grass => "Grass: click to plant a patch, rooted where clicked",
            EditorPlacementMode.Flare => "Flares: click to place a lens flare",
            _ => "Placement mode",
        });
    }
}

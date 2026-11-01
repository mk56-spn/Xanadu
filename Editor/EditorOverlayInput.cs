using System;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Editor;
using Xanadu.Systems.Timeline;
using Xanadu.Editor.Commands;

namespace Xanadu.Editor;

/// <summary>Runs the editor's key bindings; <see cref="EditorShortcuts"/> says which key does what.</summary>
public partial class EditorOverlay : CanvasLayer, IEditorShortcutTarget
{
    public bool IsOpen => !_closed;

    public EditorShortcutScope ActiveScopes => EditorShortcuts.ActiveScopes(
        AnyDialogVisible(),
        IsBindingsHelpVisible(),
        IsWarningOpen(),
        !FindFocusedTextField().IsNull,
        IsGroupTargetEditing(),
        IsGroupOverlayOpen());

    public void Run(EditorShortcuts.Binding binding)
    {
        switch (binding.Action)
        {
            case EditorShortcut.Preview:
                StartPreview();
                break;
            case EditorShortcut.ToggleShaders:
                ToggleShaderDialog();
                break;
            case EditorShortcut.ToggleHelp:
                ToggleBindingsHelp();
                break;
            case EditorShortcut.FitView:
                _editor.GetComponent<EditorViewNavigation>().FitRequested = true;
                break;
            case EditorShortcut.Save:
                SaveCurrent();
                break;
            case EditorShortcut.Undo:
                Undo();
                break;
            case EditorShortcut.Redo:
                Redo();
                break;
            case EditorShortcut.Copy:
                _commands.Enqueue(EditorCommand.CopySelection());
                break;
            case EditorShortcut.Paste:
                _commands.Enqueue(EditorCommand.PasteSelection());
                break;
            case EditorShortcut.CloseTarget:
                CloseGroupTarget();
                break;
            case EditorShortcut.ReopenTargetedGroup:
                ReopenTargetedGroup();
                break;
            case EditorShortcut.CloseGroupLayer:
                if (IsGroupPaletteOpen())
                    CloseGroupPalette();
                else
                    CloseGroupOverlay();
                break;
            case EditorShortcut.Timeline:
                if (!_editor.IsNull && _editor.TryGetComponent(out TimelineHost timeline))
                    TimelineShortcuts.Run(timeline.Host, binding.Timeline);
                break;
            case EditorShortcut.Place:
                SetPlacementMode(binding.Placement);
                break;
            case EditorShortcut.Back:
                _onBack?.Invoke();
                break;
            case EditorShortcut.Forward:
                _onForward?.Invoke();
                break;
            case EditorShortcut.TapTempo:
                TapTempo();
                break;
            case EditorShortcut.OpenSong:
                OpenSongDialog();
                break;
            case EditorShortcut.Delete:
                DeleteSelectedNote();
                break;
            case EditorShortcut.MoveLeft:
                _commands.Enqueue(EditorCommand.MoveSelection(-1));
                break;
            case EditorShortcut.MoveRight:
                _commands.Enqueue(EditorCommand.MoveSelection(1));
                break;
        }
    }

    private void DeleteSelectedNote()
    {
        if (_editor.IsNull)
            return;

        _commands.Enqueue(EditorCommand.DeleteSelection());
    }

    private void SetPlacementMode(EditorPlacementMode mode)
    {
        if (_editor.IsNull)
            return;

        _commands.Enqueue(EditorCommand.SetPlacementMode(mode));
        SetStatus(mode switch
        {
            EditorPlacementMode.Notes => "Place normal",
            EditorPlacementMode.Pair => "Place pair",
            EditorPlacementMode.Dash => "Place dash",
            EditorPlacementMode.Momentum => "Place momentum",
            EditorPlacementMode.Blocks => "Place blocks",
            EditorPlacementMode.Spikes => "Place spikes",
            EditorPlacementMode.Contact => "Place contact",
            EditorPlacementMode.CharacterSwitch => "Place character switch",
            EditorPlacementMode.WeaponSwitch => "Place weapon switch",
            EditorPlacementMode.Channel => "Place channel start",
            EditorPlacementMode.FormStep => "Place Form step",
            _ => "Placement mode",
        });
    }

    private void Undo()
    {
        if (_editor.IsNull)
            return;

        _commands.Enqueue(EditorCommand.Undo());
        SetStatus("Undo");
    }

    private void Redo()
    {
        if (_editor.IsNull)
            return;

        _commands.Enqueue(EditorCommand.Redo());
        SetStatus("Redo");
    }
}

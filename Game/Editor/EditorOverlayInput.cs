using System;
using Xanadu.Ecs;
using Xanadu.Systems.Editor;
using Xanadu.Systems.Timeline;
using Xanadu.Editor.Commands;

namespace Xanadu.Editor;

/// <summary>Runs the editor's key bindings; <see cref="EditorShortcuts"/> says which key does what.</summary>
public sealed partial class EditorOverlay : IEditorShortcutTarget
{
    public bool IsOpen => !_closed;

    public EditorShortcutScope ActiveScopes => EditorShortcuts.ActiveScopes(
        AnyDialogVisible(),
        IsBindingsHelpVisible(),
        IsWarningOpen(),
        !FindFocusedTextField().IsNull,
        IsGroupTargetEditing(),
        IsGroupOverlayOpen(),
        IsDraftingPolygon());

    private bool IsDraftingPolygon() =>
        !_editor.IsNull && _editor.TryGetComponent(out EditorPolygonDraft draft) && draft.Active;

    public void Run(EditorShortcuts.Binding binding)
    {
        switch (binding.Action)
        {
            case EditorShortcut.Preview:
                StartPreview();
                break;
            case EditorShortcut.PreviewHere:
                StartPreview(fromPlayhead: true);
                break;
            case EditorShortcut.ToggleShaders:
                ToggleShaderDialog();
                break;
            case EditorShortcut.CloseDialog:
                // The song picker can't be closed while the level has no song: Escape leaves instead.
                if (EditorDialogs.IsSongRequired(_entities) && EditorDialogs.IsOpen(_entities, EditorDialogKind.Song))
                    _onBack?.Invoke();
                else
                    EditorDialogs.Dismiss(_entities);
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
            case EditorShortcut.FinishPolygon:
                _editor.GetComponent<EditorPolygonDraft>().FinishRequested = true;
                break;
            case EditorShortcut.CancelPolygon:
                _editor.GetComponent<EditorPolygonDraft>().CancelRequested = true;
                break;
            case EditorShortcut.Back:
                _onBack?.Invoke();
                break;
            case EditorShortcut.Forward:
                _onForward?.Invoke();
                break;
            case EditorShortcut.Delete:
                DeleteSelectedNote();
                break;
            case EditorShortcut.RotateSelection:
                _commands.Enqueue(EditorCommand.RotateSelection());
                break;
            case EditorShortcut.FlipHorizontal:
                _commands.Enqueue(EditorCommand.FlipSelection(horizontal: true));
                break;
            case EditorShortcut.FlipVertical:
                _commands.Enqueue(EditorCommand.FlipSelection(horizontal: false));
                break;
            case EditorShortcut.CycleOneWay:
                _commands.Enqueue(EditorCommand.CycleOneWay());
                break;
            case EditorShortcut.MoveLeft:
                _commands.Enqueue(EditorCommand.MoveSelection(-1));
                break;
            case EditorShortcut.MoveRight:
                _commands.Enqueue(EditorCommand.MoveSelection(1));
                break;
            case EditorShortcut.MoveForward:
                _commands.Enqueue(EditorCommand.MoveInLayer(1));
                break;
            case EditorShortcut.MoveBack:
                _commands.Enqueue(EditorCommand.MoveInLayer(-1));
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
            EditorPlacementMode.Notes => "Place action note",
            EditorPlacementMode.Neutral => "Place neutral note",
            EditorPlacementMode.Dash => "Place dash",
            EditorPlacementMode.Momentum => "Place momentum",
            EditorPlacementMode.Blocks => "Place blocks",
            EditorPlacementMode.Spikes => "Place spikes",
            EditorPlacementMode.Contact => "Place contact barrier",
            EditorPlacementMode.Text => "Place text",
            EditorPlacementMode.Grass => "Place grass",
            EditorPlacementMode.Flare => "Place flare",
            EditorPlacementMode.FrictionBar => "Place friction field",
            EditorPlacementMode.Polygon => "Draw a polygon: click its points, then the first again",
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

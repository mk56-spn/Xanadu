using Xanadu.Model.Ecs.Components;
using Xanadu.Editor.Commands;
using Xanadu.Systems.Editor;
using Xanadu.Ui.Systems.Timeline;

namespace Xanadu.Editor;

/// <summary>Runs the editor's key bindings; <see cref="EditorShortcuts" /> says which key does what.</summary>
public sealed partial class EditorOverlay : IEditorShortcutTarget
{
    public bool IsOpen => !_closed;

    public EditorShortcutScope ActiveScopes => EditorShortcuts.ActiveScopes(
        AnyDialogVisible(),
        IsBindingsHelpVisible(),
        IsWarningOpen(),
        !FindFocusedTextField().IsNull,
        IsDrafting());

    public void Run(EditorShortcuts.Binding binding)
    {
        switch (binding.Action)
        {
            case EditorShortcut.Preview:
                StartPreview();
                break;
            case EditorShortcut.PreviewHere:
                StartPreview(true);
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
            case EditorShortcut.Timeline:
                if (!_editor.IsNull && _editor.TryGetComponent(out TimelineHost timeline))
                    TimelineShortcuts.Run(timeline.Host, binding.Timeline);
                break;
            case EditorShortcut.Place:
                SetPlacementMode(binding.Placement);
                break;
            case EditorShortcut.FinishDraft:
                _editor.GetComponent<EditorSpanDraft>().FinishRequested = true;
                break;
            case EditorShortcut.CancelDraft:
                _editor.GetComponent<EditorSpanDraft>().CancelRequested = true;
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
            case EditorShortcut.MoveLeft:
                _commands.Enqueue(EditorCommand.MoveSelection(-1));
                break;
            case EditorShortcut.MoveRight:
                _commands.Enqueue(EditorCommand.MoveSelection(1));
                break;
        }
    }

    private bool IsDrafting() =>
        !_editor.IsNull && _editor.TryGetComponent(out EditorSpanDraft draft) && draft.Active;

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
            EditorPlacementMode.Curve => "Route notes: click the head, move the playhead, click each turn, right click ends",
            EditorPlacementMode.Hold => "Hold notes: click the head, move the playhead, click the tail",
            EditorPlacementMode.Dash => "Dash notes: press and drag out how far it teleports",
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

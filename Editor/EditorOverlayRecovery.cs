using System;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor.Commands;
using Xanadu.Editor.Recovery;

namespace Xanadu.Editor;

/// <summary>Asks whether to restore autosaved edits the editor found when it opened a level.</summary>
public partial class EditorOverlay : CanvasLayer
{
    private ConfirmationDialog _recoveryDialog = null!;

    private void BuildRecoveryDialog()
    {
        _recoveryDialog = new ConfirmationDialog
        {
            Title = "Recover unsaved changes",
            OkButtonText = "Recover",
            CancelButtonText = "Discard",
            Exclusive = true,
        };
        _recoveryDialog.Confirmed += () => AnswerRecovery(restore: true);
        _recoveryDialog.Canceled += () => AnswerRecovery(restore: false);
        AddChild(_recoveryDialog);
    }

    private void ProcessRecoveryOffer()
    {
        if (!_editor.HasComponent<EditorRecoveryOffer>() || _recoveryDialog.Visible)
            return;

        ref EditorRecoveryOffer offer = ref _editor.GetComponent<EditorRecoveryOffer>();
        if (offer.Asked)
            return;

        offer.Asked = true;
        string name = string.IsNullOrEmpty(offer.Path)
            ? "this untitled level"
            : System.IO.Path.GetFileName(offer.Path);
        _recoveryDialog.DialogText =
            $"Found edits to {name} that were never saved ({offer.SavedAt.ToLocalTime():g}).\n" +
            "Recover them, or discard and keep the saved version?";
        _recoveryDialog.PopupCentered(new Vector2I(520, 160));
    }

    private bool IsRecoveryDialogVisible() => _recoveryDialog?.Visible ?? false;

    private void AnswerRecovery(bool restore)
    {
        if (_editor.IsNull || !_editor.HasComponent<EditorRecoveryOffer>())
            return;

        EditorRecoveryOffer offer = _editor.GetComponent<EditorRecoveryOffer>();
        _editor.RemoveComponent<EditorRecoveryOffer>();

        try
        {
            if (restore)
            {
                _commands.Enqueue(EditorCommand.RecoverLevel(offer.Level, offer.Path));
                SetStatus("Recovered unsaved changes");
            }
            else
            {
                LevelRecoveryStore.Default.Delete(offer.Path);
            }
        }
        catch (Exception exception)
        {
            SetStatus($"Recovery failed: {exception.Message}");
        }
    }
}

using Xanadu.Ecs;

namespace Xanadu.Editor;

/// <summary>Editor overlay per-frame work, run by its host after the systems update.</summary>
public sealed partial class EditorOverlay
{
    private bool _songDialogChecked;

    /// <summary>Handles the editor's UI requests and holds the pointer while a host dialog or the key list is up.</summary>
    public void Tick()
    {
        if (_closed)
            return;

        if (_editor.IsNull)
            _editor = FindEditor();

        if (_editor.IsNull)
            return;

        // A level without a song asks for one, once, on the first frame.
        if (!_songDialogChecked)
        {
            _songDialogChecked = true;
            if (_openSongDialog &&
                _editor.HasComponent<LevelInfo>() &&
                _editor.GetComponent<LevelInfo>().Song is null)
            {
                OpenSongDialog();
            }
        }

        ProcessUiRequest();
        _pointer?.SetBlocked(NativeDialogVisible() || IsBindingsHelpVisible());
    }
}

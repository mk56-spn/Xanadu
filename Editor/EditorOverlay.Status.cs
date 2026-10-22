using System.IO;
using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;

namespace Writer.Editor;

/// <summary>Editor overlay per-frame status and visibility updates.</summary>
public partial class EditorOverlay : CanvasLayer
{
    public override void _Process(double delta)
    {
        if (_closed || _editor.IsNull)
            return;

        ProcessUiRequest();

        if (_suppressPointerUntilRelease &&
            !Godot.Input.IsMouseButtonPressed(MouseButton.Left))
        {
            _suppressPointerUntilRelease = false;
        }

        _pointer?.SetBlocked(
            AnyDialogVisible() ||
            _metadataPanel.Visible ||
            _suppressPointerUntilRelease);

        ref Playhead playhead = ref _editor.GetComponent<Playhead>();
        ref LevelInfo level = ref _editor.GetComponent<LevelInfo>();

        string levelName = string.IsNullOrWhiteSpace(level.Name)
            ? "Untitled"
            : level.Name;
        if (_levelLabel.Text != levelName)
            _levelLabel.Text = levelName;

        string song = string.IsNullOrEmpty(level.AudioPath)
            ? "No song loaded"
            : Path.GetFileName(level.AudioPath);
        string songText = $"Song: {song}";
        if (_songLabel.Text != songText)
            _songLabel.Text = songText;

        string timeText =
            $"{FormatMs(playhead.TimeMs)} / {FormatMs(playhead.DurationMs)}";
        if (_timeLabel.Text != timeText)
            _timeLabel.Text = timeText;

        _notesLabel.Text = $"{_entities.Query<Note>().Count} notes";

        bool previewActive = _editor.HasComponent<EditorPreview>() &&
                             _editor.GetComponent<EditorPreview>().Active;

        if (previewActive != _previewActive)
        {
            _previewActive = previewActive;
            _previewLabel.Visible = previewActive;
            UpdateEditControlsVisibility();
        }

        bool confirmationActive = IsSceneConfirmationOpen();
        if (confirmationActive != _confirmationActive)
        {
            _confirmationActive = confirmationActive;
            UpdateEditControlsVisibility();
        }

        if (previewActive &&
            !playhead.Playing &&
            playhead.DurationMs > 0 &&
            playhead.TimeMs >= playhead.DurationMs)
        {
            EndPreview();
            _statusLabel.Text = "Preview ended";
        }

        if (_previewLabel.Visible)
        {
            _previewLabel.Text =
                $"PREVIEW - {level.Name}   " +
                $"{playhead.TimeMs / 1000f:0.00}s / {playhead.DurationMs / 1000f:0.00}s   Esc to return";
        }

        if (!_nameEdit.HasFocus() && _nameEdit.Text != level.Name)
        {
            _syncing = true;
            _nameEdit.Text = level.Name;
            _syncing = false;
        }

        if (!_artistEdit.HasFocus() && _artistEdit.Text != level.Artist)
        {
            _syncing = true;
            _artistEdit.Text = level.Artist;
            _syncing = false;
        }

        if (!_difficultyEdit.HasFocus() && _difficultyEdit.Text != level.Difficulty)
        {
            _syncing = true;
            _difficultyEdit.Text = level.Difficulty;
            _syncing = false;
        }

        string bpm = playhead.Bpm.ToString();
        if (!_bpmEdit.HasFocus() && _bpmEdit.Text != bpm)
        {
            _syncing = true;
            _bpmEdit.Text = bpm;
            _syncing = false;
        }

        string offset = playhead.OffsetMs.ToString();
        if (!_offsetEdit.HasFocus() && _offsetEdit.Text != offset)
        {
            _syncing = true;
            _offsetEdit.Text = offset;
            _syncing = false;
        }

    }

    private bool AnyDialogVisible() =>
        _songDialog.Visible ||
        _saveAsDialog.Visible ||
        _loadDialog.Visible;

    private void SetEditControlsVisible(bool visible)
    {
        _header.Visible = visible;
        _footer.Visible = visible;

        if (!visible)
            HideMetadata();
    }

    private void UpdateEditControlsVisibility() =>
        SetEditControlsVisible(!_previewActive && !_confirmationActive);

    private bool IsSceneConfirmationOpen()
    {
        foreach (Entity entity in _entities.Query<SceneConfirmation>().ToEntityList())
        {
            if (entity.GetComponent<SceneConfirmation>().Visible)
                return true;
        }

        return false;
    }

    private bool IsWarningOpen()
    {
        foreach (Entity entity in _entities.Query<WarningPopup>().ToEntityList())
        {
            if (entity.GetComponent<WarningPopup>().Visible)
                return true;
        }

        return false;
    }
}

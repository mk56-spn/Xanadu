using Godot;
using Xanadu.Levels;
using Xanadu.Ecs;
using Xanadu.Editor.Commands;

namespace Xanadu.Editor;

/// <summary>Editor overlay command and preview controls.</summary>
public partial class EditorOverlay : CanvasLayer
{
    private void ProcessUiRequest()
    {
        if (!_editor.HasComponent<EditorUiRequest>())
            return;

        ref EditorUiRequest request = ref _editor.GetComponent<EditorUiRequest>();
        EditorButtonKind kind = request.Kind;
        if (kind == EditorButtonKind.None)
            return;

        request.Kind = EditorButtonKind.None;

        switch (kind)
        {
            case EditorButtonKind.Save:
                SaveCurrent();
                break;

            case EditorButtonKind.SaveAs:
                OpenSaveAsDialog();
                break;

            case EditorButtonKind.LoadLevel:
                OpenLoadDialog();
                break;

            case EditorButtonKind.LoadSong:
                OpenSongDialog();
                break;

            case EditorButtonKind.Preview:
                StartPreview();
                break;
        }
    }

    private void ToggleMetronome()
    {
        if (_editor.IsNull || !_editor.HasComponent<EditorMetronome>())
            return;

        EditorMetronome metronome = _editor.GetComponent<EditorMetronome>();
        _commands.Enqueue(EditorCommand.SetMetronome(!metronome.Enabled));
    }

    private void TapTempo()
    {
        if (_editor.IsNull)
            return;

        int? bpm = _tapTempo.Tap((long)Time.GetTicksMsec());
        if (bpm is not int value)
        {
            SetStatus("Tap again for BPM");
            return;
        }

        _commands.Enqueue(EditorCommand.SetBpm(value));
        SetStatus($"Tap BPM {value}");
    }

    /// <summary>Opens the real play scene on the current level, from a start point.</summary>
    private void StartPreview()
    {
        if (_editor.IsNull || _onPreview is null || !ValidateCurrent())
            return;

        LevelDocument document = LevelDocumentMapper.Capture(_entities, _editor);
        long playheadMs = _editor.GetComponent<Playhead>().TimeMs;
        _onPreview(document, CurrentLevelPath(), playheadMs);
    }
}

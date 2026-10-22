using Godot;
using Writer.Ecs;
using Writer.Editor.Commands;

namespace Writer.Editor;

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

    private void TogglePreview()
    {
        if (_editor.IsNull)
            return;

        if (IsPreviewing())
        {
            EndPreview();
            return;
        }

        if (!ValidateCurrent())
            return;

        _commands.Enqueue(EditorCommand.BeginPreview());
    }

    private void EndPreview()
    {
        if (_editor.IsNull)
            return;

        _commands.Enqueue(EditorCommand.EndPreview());
    }

    private bool IsPreviewing() =>
        !_editor.IsNull &&
        _editor.HasComponent<EditorPreview>() &&
        _editor.GetComponent<EditorPreview>().Active;
}

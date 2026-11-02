using System;
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

            case EditorButtonKind.PreviewHere:
                StartPreview(fromPlayhead: true);
                break;

            case EditorButtonKind.BackgroundColor:
                OpenBackgroundDialog();
                break;

            case EditorButtonKind.ResetBackgroundColor:
                _commands.Enqueue(EditorCommand.SetBackgroundColor(null));
                break;
        }
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

    /// <summary>Opens the real play scene on the current level, from a start point or the playhead.</summary>
    private void StartPreview(bool fromPlayhead = false)
    {
        Action<LevelDocument, string, long>? launch = fromPlayhead ? _onPreviewHere : _onPreview;
        if (_editor.IsNull || launch is null)
            return;

        // Preview rebuilds the editor from its in-memory level when it returns.
        // Saving first makes that copy the clean state, so the autosave file is
        // cleared instead of being offered back as a recovery on every preview.
        if (!SaveCurrent(out string path))
            return;

        LevelDocument document = LevelDocumentMapper.Capture(_entities, _editor);
        long playheadMs = _editor.GetComponent<Playhead>().TimeMs;
        launch(document, path, playheadMs);
    }
}

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

        // Only take the requests handled here: others, such as LEVEL INFO's
        // toggle, belong to an ECS system that may not have run yet this frame.
        if (!Handles(kind))
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
                EditorDialogs.Open(_entities, EditorDialogKind.MapColor, (int)EditorColorTarget.Background);
                break;

            case EditorButtonKind.ResetBackgroundColor:
                _commands.Enqueue(EditorCommand.SetBackgroundColor(null));
                break;

            case EditorButtonKind.OutsideColor:
                EditorDialogs.Open(_entities, EditorDialogKind.MapColor, (int)EditorColorTarget.Outside);
                break;

            case EditorButtonKind.ResetOutsideColor:
                _commands.Enqueue(EditorCommand.SetOutsideColor(null));
                break;

            case EditorButtonKind.FrameColor:
                EditorDialogs.Open(_entities, EditorDialogKind.MapColor, (int)EditorColorTarget.Frame);
                break;

            case EditorButtonKind.ResetFrameColor:
                _commands.Enqueue(EditorCommand.SetFrameColor(null));
                break;

            case EditorButtonKind.ImportShader:
                OpenShaderImport();
                break;
        }
    }

    /// <summary>The request kinds the overlay acts on and clears.</summary>
    private static bool Handles(EditorButtonKind kind) =>
        kind is EditorButtonKind.Save
            or EditorButtonKind.SaveAs
            or EditorButtonKind.LoadLevel
            or EditorButtonKind.LoadSong
            or EditorButtonKind.Preview
            or EditorButtonKind.PreviewHere
            or EditorButtonKind.BackgroundColor
            or EditorButtonKind.ResetBackgroundColor
            or EditorButtonKind.OutsideColor
            or EditorButtonKind.ResetOutsideColor
            or EditorButtonKind.FrameColor
            or EditorButtonKind.ResetFrameColor
            or EditorButtonKind.ImportShader;

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

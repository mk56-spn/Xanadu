using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;

namespace Writer.Editor;

/// <summary>Editor overlay per-frame bridge and preview lifecycle.</summary>
public partial class EditorOverlay : CanvasLayer
{
    public override void _Process(double delta)
    {
        if (_closed || _editor.IsNull)
            return;

        ProcessUiRequest();
        SyncTextFieldBridge();
        _pointer?.SetBlocked(AnyDialogVisible());

        if (!_editor.HasComponent<Playhead>() ||
            !_editor.HasComponent<EditorPreview>())
        {
            return;
        }

        Playhead playhead = _editor.GetComponent<Playhead>();
        EditorPreview preview = _editor.GetComponent<EditorPreview>();
        if (preview.Active &&
            !playhead.Playing &&
            playhead.DurationMs > 0 &&
            playhead.TimeMs >= playhead.DurationMs)
        {
            EndPreview();
            SetStatus("Preview ended");
        }
    }
}

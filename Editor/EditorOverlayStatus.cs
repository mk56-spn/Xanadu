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
        _pointer?.SetBlocked(AnyDialogVisible() || IsBindingsHelpVisible());
    }
}

using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Editor;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Editor;

/// <summary>F2 editor-binding reference overlay toggle.</summary>
public partial class EditorOverlay : CanvasLayer
{

    private void ToggleBindingsHelp()
    {
        if (_editor.IsNull || !_editor.HasComponent<EditorBindingsHelp>())
            return;

        EditorBindingsHelp help = _editor.GetComponent<EditorBindingsHelp>();
        Entity panel = help.Panel;
        if (panel.IsNull || !panel.HasComponent<Position>())
            return;

        panel.GetComponent<Position>().Value = help.Home;
        panel.AddTag<Moved>();
        UiLayout.SetHidden(panel, !UiLayout.IsHidden(panel));
    }

    private bool IsBindingsHelpVisible() =>
        !_editor.IsNull &&
        _editor.HasComponent<EditorBindingsHelp>() &&
        !UiLayout.IsHidden(_editor.GetComponent<EditorBindingsHelp>().Panel);
}

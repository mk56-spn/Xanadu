using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Editor;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Editor;

/// <summary>F2 editor-binding reference overlay toggle.</summary>
public partial class EditorOverlay : CanvasLayer
{
    private static readonly Vector2 HiddenBindings = new(-10_000, -10_000);

    private void ToggleBindingsHelp()
    {
        if (_editor.IsNull || !_editor.HasComponent<EditorBindingsHelp>())
            return;

        ref EditorBindingsHelp help = ref _editor.GetComponent<EditorBindingsHelp>();
        help.Visible = !help.Visible;
        if (help.Panel.IsNull || !help.Panel.HasComponent<Position>())
            return;

        ref Position position = ref help.Panel.GetComponent<Position>();
        position.Value = help.Visible ? help.Home : HiddenBindings;
        help.Panel.AddTag<Moved>();
    }

    private bool IsBindingsHelpVisible() =>
        !_editor.IsNull &&
        _editor.HasComponent<EditorBindingsHelp>() &&
        _editor.GetComponent<EditorBindingsHelp>().Visible;
}

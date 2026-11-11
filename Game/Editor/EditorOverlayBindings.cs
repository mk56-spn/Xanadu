using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Systems.Editor;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Editor;

/// <summary>F9 editor-binding reference overlay toggle.</summary>
public sealed partial class EditorOverlay
{

    private void ToggleBindingsHelp()
    {
        Entity panel = BindingsHelpPanel();
        if (panel.IsNull || !panel.HasComponent<Position>())
            return;

        panel.GetComponent<Position>().Value = panel.GetComponent<EditorPanel>().Home;
        panel.AddTag<Moved>();
        UiLayout.SetHidden(panel, !UiLayout.IsHidden(panel));
    }

    private bool IsBindingsHelpVisible() =>
        BindingsHelpPanel() is { IsNull: false } panel && !UiLayout.IsHidden(panel);

    private Entity BindingsHelpPanel() =>
        _editor.IsNull ? default : EditorPanel.Find(_editor.Store, EditorPanelKind.Bindings);
}

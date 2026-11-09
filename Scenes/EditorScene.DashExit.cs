using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Systems.Editor;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Scenes;

/// <summary>
/// The dash tool's exit section of the left panel: which way the selected dash
/// notes push the player once they land, and how hard; the brush's when none
/// is selected. With no push the player hangs where the dash put it.
/// </summary>
internal static class EditorSceneDashExitPanel
{
    private const float RowHeight = 28;

    internal static void Build(UiNode panel, Entity editor, float inner)
    {
        UiNode section = EditorSceneUi.ToolSection(panel, inner, EditorToolSections.DashExit);
        EditorSceneUi.SidebarLabel(section, "DASH EXIT", inner, 12);

        var readouts = new Entity[System.Enum.GetValues<DashExitSetting>().Length];
        Row(section, inner, readouts, DashExitSetting.Direction,
            "Turn the push anticlockwise",
            "Turn the push clockwise");
        Row(section, inner, readouts, DashExitSetting.Speed,
            "Weaker push; at off the player hangs after the dash",
            "Stronger push out of the dash");
        editor.AddComponent(new EditorDashExitPanel(readouts));
    }

    private static void Row(UiNode section, float inner, Entity[] readouts, DashExitSetting setting, string less, string more)
    {
        UiNode row = section.Row().Size(inner, RowHeight).Gap(UiTheme.ContainerSpacing).Main(StackMainAlignment.Start);
        readouts[(int)setting] = row.Text(
                EditorDashExitPanelSystem.Label(setting, DashDirection.Right, 0f),
                UiTheme.SmallFontSize,
                SceneUi.DimTextColor,
                TextAlign.Left)
            .Size(inner - (RowHeight + UiTheme.ContainerSpacing) * 2, RowHeight);
        row.Button("-", 13, SceneUi.MutedButtonColor)
            .Size(RowHeight, RowHeight)
            .With(new EditorButton(EditorButtonKind.DashExitStep, EditorDashExitPanelSystem.StepArg(setting, -1)))
            .Tooltip(less);
        row.Button("+", 13, SceneUi.MutedButtonColor)
            .Size(RowHeight, RowHeight)
            .With(new EditorButton(EditorButtonKind.DashExitStep, EditorDashExitPanelSystem.StepArg(setting, 1)))
            .Tooltip(more);
    }
}

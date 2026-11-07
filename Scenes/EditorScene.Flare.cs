using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Systems.Editor;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Scenes;

/// <summary>
/// The flare tool's section of the left panel: the selected flare's settings,
/// or the brush's when none is selected. Stepper rows under three
/// subheadings, then the colour.
/// </summary>
internal static class EditorSceneFlarePanel
{
    private const float RowHeight = 28;

    internal static void Build(UiNode panel, Entity editor, float inner)
    {
        UiNode section = EditorSceneUi.ToolSection(panel, inner, EditorToolSections.Flare);
        EditorSceneUi.SidebarLabel(section, "FLARE", inner, 12);

        var readouts = new Entity[System.Enum.GetValues<FlareSetting>().Length];
        Subhead(section, "BOX", inner);
        Rows(section, inner, readouts, FlareSetting.Width, FlareSetting.Height);
        Subhead(section, "STREAKS", inner);
        Rows(section, inner, readouts, FlareSetting.Axes, FlareSetting.Thickness, FlareSetting.Falloff);
        Subhead(section, "LIGHT", inner);
        Rows(section, inner, readouts, FlareSetting.Core, FlareSetting.Halo, FlareSetting.Glow, FlareSetting.Intensity, FlareSetting.Heat);

        UiNode color = section.ColorField(
            "LIGHT",
            inner,
            "The flare's colour; the core and the streaks' middles run towards white by the heat. Self modulate multiplies it, and a group's colour fade overrides it.");
        editor.AddComponent(new EditorFlarePanel(section.Entity, readouts, color.Entity));
    }

    private static void Subhead(UiNode section, string text, float inner)
    {
        EditorSceneUi.Separator(section, inner);
        section.Text(text, UiTheme.SmallFontSize, SceneUi.DimTextColor, TextAlign.Left).Size(inner, 16);
    }

    private static void Rows(UiNode section, float inner, Entity[] readouts, params FlareSetting[] settings)
    {
        foreach (FlareSetting setting in settings)
        {
            (string less, string more) = Tooltips(setting);
            UiNode row = section.Row().Size(inner, RowHeight).Gap(UiTheme.ContainerSpacing).Main(StackMainAlignment.Start);
            readouts[(int)setting] = row.Text(
                    EditorFlarePanelSystem.Label(setting, new LevelFlare()),
                    UiTheme.SmallFontSize,
                    SceneUi.DimTextColor,
                    TextAlign.Left)
                .Size(inner - (RowHeight + UiTheme.ContainerSpacing) * 2, RowHeight);
            row.Button("-", 13, SceneUi.MutedButtonColor)
                .Size(RowHeight, RowHeight)
                .With(new EditorButton(EditorButtonKind.FlareStep, EditorFlarePanelSystem.StepArg(setting, -1)))
                .Tooltip(less);
            row.Button("+", 13, SceneUi.MutedButtonColor)
                .Size(RowHeight, RowHeight)
                .With(new EditorButton(EditorButtonKind.FlareStep, EditorFlarePanelSystem.StepArg(setting, 1)))
                .Tooltip(more);
        }
    }

    private static (string Less, string More) Tooltips(FlareSetting setting) => setting switch
    {
        FlareSetting.Width => ("Narrower box: a shorter first streak", "Wider box: a longer first streak"),
        FlareSetting.Height => ("Lower box: a shorter second streak", "Taller box: a longer second streak"),
        FlareSetting.Axes => ("One streak, along the width", "Two streaks, along the width and the height"),
        FlareSetting.Thickness => ("Thinner streaks", "Thicker streaks"),
        FlareSetting.Falloff => ("Streaks hold their light further out", "Streaks fade sooner towards their ends"),
        FlareSetting.Core => ("A smaller core", "A bigger core"),
        FlareSetting.Halo => ("A tighter halo", "A wider halo"),
        FlareSetting.Glow => ("A fainter halo; at 0 there is none", "A brighter halo"),
        FlareSetting.Intensity => ("A dimmer flare", "A brighter flare; past 1 it blooms outwards"),
        _ => ("Core keeps the flare's colour", "Core runs hotter, towards white"),
    };
}

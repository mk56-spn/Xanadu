using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Settings;
using Xanadu.Systems.Editor;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Scenes;

/// <summary>The small UI builders every editor panel shares: labels, sections, dropdowns and text.</summary>
internal static class EditorSceneUi
{
internal static UiNode ToolSection(UiNode panel, float width, EditorPlacementMode[] modes) =>
        panel.Column()
            .Width(width)
            .AutoHeight()
            .Gap(UiTheme.ContainerSpacing)
            .Main(StackMainAlignment.Start)
            .With(new EditorToolSection(modes));

internal static Entity SidebarLabel(
        UiNode parent,
        string text,
        float width,
        int fontSize) =>
        parent.Text(text, fontSize, SceneUi.DimTextColor, TextAlign.Center)
            .Size(width, fontSize + 10);

    /// <summary>
    /// The floating "note elements" box at the viewport's top-right corner: the
    /// keys new notes get, and under them the direction wheel for the kinds that
    /// have a direction. The box shrinks around whatever is showing, and
    /// <see cref="EditorNoteTypeBarSystem"/> hides it for items with no key.
    /// </summary>

internal static Entity BuildDropdown(
        UiNode parent,
        float width,
        EditorDropdownKind kind,
        string[] labels,
        int selected,
        float height = 34f)
    {
        UiNode button = parent.Button("", 13, SceneUi.EditorButtonColor).Size(width, height);
        parent.DropdownList(
            button.Entity,
            labels,
            selected,
            optionColor: _ => SceneUi.MutedButtonColor);
        return button.With(new EditorDropdownControl(kind)).Entity;
    }

internal static void SetDropdownSelected(Entity button, int selected)
    {
        if (button.IsNull || !button.HasComponent<Dropdown>())
            return;

        ref Dropdown dropdown = ref button.GetComponent<Dropdown>();
        dropdown.Selected = selected;
        dropdown.Open = false;
    }

internal static Entity HudText(
        UiNode parent,
        Vector2 center,
        Vector2 size,
        string text,
        int fontSize,
        Color color) =>
        parent.Text(text, fontSize, color, TextAlign.Left)
            .Size(size)
            .At(center);
}

using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Model.Ecs.Components;
using Position = Xanadu.Core.Position;
using Xanadu.Model.Settings;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Model.Playfields;
using Xanadu.Model.Editor;
using Xanadu.Logic.Editor;
using Xanadu.Screens.Shared;

namespace Xanadu.Screens.Editor;

/// <summary>Creates the initial editor state and a playfield centered between the header and footer.</summary>
public static class EditorSceneSidebar
{
    private const float PanelPadding = UiTheme.ContainerPadding;
    private const float LeftPanelWidth = 210;

    /// <summary>
    /// The tool strip at the left edge: one button per entry of
    /// <see cref="EditorToolGroups.All"/>. A lone tool is picked straight from
    /// it; a group opens a flyout of its tools beside the strip, on a layer of
    /// its own so it covers the tool settings.
    /// </summary>
    internal static void BuildToolStrip(UiNode ui, UiNode flyouts, EditorShellRects shell)
    {
        float pad = (EditorShellMetrics.ToolStripWidth - EditorShellMetrics.ToolButton) / 2;
        float gap = EditorShellMetrics.ToolGap;
        UiNode strip = ui.Column()
            .Size(shell.ToolStrip.Size)
            .Gap(gap)
            .Fill(SceneUi.PanelColor)
            .Pad(pad)
            .Input()
            .Main(StackMainAlignment.Start);

        // The buttons shrink when there are more of them than fit the strip at full size.
        int count = EditorToolGroups.All.Length;
        float fits = (shell.ToolStrip.Size.Y - pad * 2 + gap) / count - gap;
        float side = Math.Min(EditorShellMetrics.ToolButton, MathF.Floor(fits));
        var size = new Vector2(side, side);

        for (int index = 0; index < count; index++)
        {
            EditorToolGroupInfo group = EditorToolGroups.All[index];
            if (!group.IsGroup)
            {
                EditorTool tool = group.Tools[0];
                ToolButton(strip, size, tool.Icon, ToolTooltip(tool), EditorPlacementKeybinds.LabelFor(tool.Mode), EditorButton.Place(tool.Mode));
                continue;
            }

            // The flyout lines up with its button, just right of the strip.
            float top = shell.ToolStrip.Position.Y + pad + index * (side + gap);
            var flyoutBox = new Rect2(
                shell.ToolStrip.End.X + gap,
                top - pad,
                group.Tools.Length * (side + gap) - gap + pad * 2,
                side + pad * 2);
            UiNode flyout = flyouts.Row()
                .Box(flyoutBox)
                .Gap(gap)
                .Pad(pad)
                .Fill(SceneUi.PanelColor)
                .Input()
                .Main(StackMainAlignment.Start)
                .Hidden();
            foreach (EditorTool tool in group.Tools)
                ToolButton(flyout, size, tool.Icon, ToolTooltip(tool), EditorPlacementKeybinds.LabelFor(tool.Mode), EditorButton.Place(tool.Mode));

            // The corner mark says the button opens more tools.
            (UiNode button, Entity icon) = ToolButton(strip, size, group.Icon ?? group.Tools[0].Icon, group.Name, "◢", EditorButton.Of(EditorButtonKind.ToolGroup, index));
            button.With(new EditorToolGroup(index, flyout.Entity, icon));
        }
    }

    private static string ToolTooltip(EditorTool tool)
    {
        string key = EditorPlacementKeybinds.LabelFor(tool.Mode);
        return key.Length > 0 ? $"{tool.Name} ({key})" : tool.Name;
    }

    /// <summary>A square tool button: its icon, and a small mark in the corner, such as its key.</summary>
    private static (UiNode Button, Entity Icon) ToolButton(
        UiNode parent,
        Vector2 size,
        string icon,
        string tooltip,
        string corner,
        UiCommand action)
    {
        // The button is a container so it can hold its mark in a corner beside the icon.
        UiNode button = parent.Column()
            .Size(size)
            .Fill(SceneUi.MutedButtonColor)
            .Input()
            .Main(StackMainAlignment.Center)
            .Cross(StackCrossAlignment.Center)
            .With(ButtonVisual.Create(SceneUi.MutedButtonColor))
            .Tooltip(tooltip)
            .With(action);
        Entity iconText = button.Text(icon, 26, UiTheme.Text, TextAlign.Center).Size(size).Entity;

        // The key in the corner, so the shortcut is learnt by looking at the tool.
        if (corner.Length > 0)
        {
            button.Text(corner, UiType.Caption, SceneUi.DimTextColor, TextAlign.Center, bold: true)
                .Size(18, 16)
                .Anchor(new Vector2(1, 1));
        }

        return (button, iconText);
    }

    /// <summary>
    /// The shell's right panel, docked to the right edge for the full height of
    /// the middle row. Empty for now: what a selected note can change comes
    /// back here as it is rebuilt.
    /// </summary>
    internal static void BuildSidebar(UiNode ui, Entity editor, EditorShellRects shell)
    {
        float inner = shell.RightPanel.Size.X - PanelPadding * 2;
        UiNode panel = ui.Column()
            .Size(shell.RightPanel.Size)
            .Gap(UiTheme.ContainerSpacing)
            .Fill(SceneUi.PanelColor)
            .Pad(PanelPadding)
            .Input()
            .Main(StackMainAlignment.Start)
            .Anchor(new Vector2(1, 0));
        panel.Collapsible("SELECTION", inner, out _);

        editor.AddComponent(new EditorSidebar(shell.RightPanel));
    }

    /// <summary>The current tool's settings, docked to the viewport's top-left corner.</summary>
    internal static void BuildLeftPanel(UiNode ui, Entity editor)
    {
        float inner = LeftPanelWidth - PanelPadding * 2;
        UiNode panel = ui.Column()
            .Width(LeftPanelWidth)
            .AutoHeight()
            .Gap(UiTheme.ContainerSpacing)
            .Fill(SceneUi.PanelColor)
            .Pad(PanelPadding)
            .Input()
            .Main(StackMainAlignment.Start)
            .Anchor(Vector2.Zero);

        UiNode speedSection = EditorSceneUi.ToolSection(panel, inner, EditorToolSections.MoveStrength);
        EditorSceneUi.SidebarLabel(speedSection, "SPEED", inner, 12);
        const float speedGap = 6;
        float speedWidth = (inner - speedGap * 2) / 3;
        UiNode speeds = speedSection.Row().Size(inner, 28).Gap(speedGap);
        for (int i = 0; i < 3; i++)
        {
            var strength = (DashStrength)i;
            speeds.Button(
                    i == 0 ? "1×" : i == 1 ? "2×" : "3×",
                    12,
                    SceneUi.MutedButtonColor)
                .Size(speedWidth, 28)
                .With(EditorButton.Strength(strength))
                .Tooltip($"The next route runs at {NoteRoute.SpeedOf(strength):0} units a second");
        }

        // Label, then - and +: the length steps by the snap, so it is discrete.
        UiNode holdSection = EditorSceneUi.ToolSection(panel, inner, EditorToolSections.HoldLength);
        EditorSceneUi.SidebarLabel(holdSection, "HOLD LENGTH", inner, 12);
        const float stepWidth = 32;
        UiNode holdRow = holdSection.Row().Size(inner, 28).Gap(speedGap);
        holdRow.Text("1 BEAT", 12, UiTheme.Text, TextAlign.Left)
            .Size(inner - (stepWidth + speedGap) * 2, 28)
            .With(new EditorHoldLengthReadout());
        holdRow.Button("-", 12, SceneUi.MutedButtonColor)
            .Size(stepWidth, 28)
            .With(EditorButton.Of(EditorButtonKind.HoldLength, -1))
            .Tooltip("Shorter by one snap step (the selected holds, else new ones)");
        holdRow.Button("+", 12, SceneUi.MutedButtonColor)
            .Size(stepWidth, 28)
            .With(EditorButton.Of(EditorButtonKind.HoldLength, 1))
            .Tooltip("Longer by one snap step (the selected holds, else new ones)");

        editor.AddComponent(new EditorLeftMenu(panel.Entity));
    }

    /// <summary>How far the note elements box sits in from the viewport's top-right corner.</summary>
    private const float NoteElementsMargin = 10;

    private const float NoteElementsBorder = 2;

    internal static void BuildNoteElements(UiNode ui, Entity editor)
    {
        const float swatchSize = 36;
        const float swatchGap = 8;
        float inner = KeybindActions.ActionKeys.Length * (swatchSize + swatchGap) - swatchGap;

        UiNode box = ui.Column()
            .Width(inner + PanelPadding * 2)
            .AutoHeight()
            .Gap(UiTheme.ContainerSpacing)
            .Fill(SceneUi.PanelColor)
            .Pad(PanelPadding)
            .Input()
            .Main(StackMainAlignment.Start)
            // Held off the viewport's corner, outlined in the picked key's colour.
            .Anchor(new Vector2(1, 0), margin: NoteElementsMargin)
            .Border(PlayfieldColors.ColorFor(KeybindActions.ActionKeys[0]), NoteElementsBorder)
            .With(new EditorNoteBarPanel());

        EditorSceneUi.SidebarLabel(box, "ACTION KEY", inner, 12);
        UiNode swatches = box.Row().Size(inner, swatchSize).Gap(swatchGap);
        foreach (KeybindAction action in KeybindActions.ActionKeys)
        {
            Color color = PlayfieldColors.ColorFor(action);
            swatches.Rect(color, size: new Vector2(swatchSize, swatchSize))
                .Input()
                .With(ButtonVisual.Create(color))
                .With(EditorButton.NoteType(action));
        }
    }
}

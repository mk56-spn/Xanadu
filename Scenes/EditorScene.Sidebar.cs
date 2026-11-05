using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Position = Xanadu.Ecs.Position;
using Xanadu.Levels;
using Xanadu.Settings;
using Xanadu.Editor;
using Xanadu.Systems.Editor;
using Xanadu.Strings;
using Xanadu.Systems.Playfields;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Scenes;

/// <summary>Creates the initial editor state and a playfield centered between the header and footer.</summary>
public static partial class EditorScene
{
    private const float PanelPadding = UiTheme.ContainerPadding;
    private const float LeftPanelWidth = 210;

    /// <summary>
    /// The tool strip at the left edge: one button per entry of
    /// <see cref="EditorToolGroups.All"/>. A lone tool is picked straight from
    /// it; a group opens a flyout of its tools beside the strip, on a layer of
    /// its own so it covers the tool settings.
    /// </summary>
    private static void BuildToolStrip(UiNode ui, UiNode flyouts, EditorShellRects shell)
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
        float side = Mathf.Min(EditorShellMetrics.ToolButton, Mathf.Floor(fits));
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
            (UiNode button, Entity icon) = ToolButton(strip, size, group.Tools[0].Icon, group.Name, "◢", new EditorButton(EditorButtonKind.ToolGroup, index));
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
        EditorButton action)
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
    /// the middle row. Its controls flow top to bottom, so an open dropdown
    /// pushes the groups down by itself.
    /// </summary>
    private static void BuildSidebar(UiNode ui, Entity editor, EditorShellRects shell)
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

        Entity characterLabel = SidebarLabel(panel, "CHARACTER", inner, 14);
        Entity character = BuildDropdown(
            panel,
            inner,
            EditorDropdownKind.Character,
            new[] { "CONTACT", "ARMED", "SPLITLINE", "FORM" },
            selected: 0);
        UiNode modulate = panel.ColorField(
            "COLOUR",
            inner,
            "Self modulate of the selection: its colour is multiplied by this, and on a block this is the interior. Type #rrggbb or #rrggbbaa; leave blank for white. A group's colour fade overrides it.");
        editor.AddComponent(new EditorSelfModulateField(modulate.Entity));
        BuildBlockOutline(panel, editor, inner);
        BuildTextPanel(panel, editor, inner);
        BuildGroupGrid(panel, editor, inner);

        editor.AddComponent(new EditorSidebar(shell.RightPanel));
        editor.AddComponent(new EditorSidebarLayout(
            panel.Entity,
            characterLabel,
            character));
    }

    /// <summary>The selected blocks' outline: its width, where 0 is none, and its own colour.</summary>
    private static void BuildBlockOutline(UiNode panel, Entity editor, float inner)
    {
        const float height = 28;
        UiNode section = panel.Column()
            .Width(inner)
            .AutoHeight()
            .Gap(UiTheme.ContainerSpacing)
            .Main(StackMainAlignment.Start)
            .Hidden();

        UiNode row = section.Row().Size(inner, height).Gap(UiTheme.ContainerSpacing).Main(StackMainAlignment.Start);
        Entity width = row.Text("OUTLINE", UiTheme.SmallFontSize, SceneUi.DimTextColor, TextAlign.Left)
            .Size(inner - (height + UiTheme.ContainerSpacing) * 2, height);
        row.Button("-", 13, SceneUi.MutedButtonColor)
            .Size(height, height)
            .With(new EditorButton(EditorButtonKind.OutlineStep, -1))
            .Tooltip("Thinner outline; at 0 there is none");
        row.Button("+", 13, SceneUi.MutedButtonColor)
            .Size(height, height)
            .With(new EditorButton(EditorButtonKind.OutlineStep, 1))
            .Tooltip("Thicker outline");

        UiNode color = section.ColorField(
            "OUTLINE",
            inner,
            "The colour of the selected blocks' and spikes' outline. A group's colour fade overrides it.");
        editor.AddComponent(new EditorBlockOutlinePanel(section.Entity, width, color.Entity));
    }

    /// <summary>The selected texts' words and size; hidden until a text is selected.</summary>
    private static void BuildTextPanel(UiNode panel, Entity editor, float inner)
    {
        const float height = 28;
        UiNode section = panel.Column()
            .Width(inner)
            .AutoHeight()
            .Gap(UiTheme.ContainerSpacing)
            .Main(StackMainAlignment.Start)
            .Hidden();

        section.Text("TEXT", UiTheme.SmallFontSize, SceneUi.DimTextColor, TextAlign.Left)
            .Size(inner, 18);
        Entity field = section.Rect(Palette.SurfaceInset, LevelText.Placeholder, new Vector2(inner, height))
            .Input()
            .With(new TextStyle(13, UiTheme.Text))
            .Align(TextAlign.Left)
            .With(new TextField(LevelText.Placeholder))
            .Tooltip("The selected text's words; click anywhere else to finish")
            .Entity;

        UiNode row = section.Row().Size(inner, height).Gap(UiTheme.ContainerSpacing).Main(StackMainAlignment.Start);
        Entity size = row.Text("SIZE", UiTheme.SmallFontSize, SceneUi.DimTextColor, TextAlign.Left)
            .Size(inner - (height + UiTheme.ContainerSpacing) * 2, height);
        row.Button("-", 13, SceneUi.MutedButtonColor)
            .Size(height, height)
            .With(new EditorButton(EditorButtonKind.TextSize, -1))
            .Tooltip("Smaller text");
        row.Button("+", 13, SceneUi.MutedButtonColor)
            .Size(height, height)
            .With(new EditorButton(EditorButtonKind.TextSize, 1))
            .Tooltip("Bigger text");

        editor.AddComponent(new EditorTextPanel(section.Entity, field, size));
    }

    /// <summary>
    /// The grass tool's section of the left panel: the selected grass's
    /// settings, or the brush's when none is selected. One stepper row per
    /// setting, then the colour.
    /// </summary>
    private static void BuildGrassPanel(UiNode panel, Entity editor, float inner)
    {
        const float height = 28;
        UiNode section = ToolSection(panel, inner, EditorToolSections.Grass);
        SidebarLabel(section, "GRASS", inner, 12);

        GrassSetting[] settings = System.Enum.GetValues<GrassSetting>();
        var readouts = new Entity[settings.Length];
        foreach (GrassSetting setting in settings)
        {
            (string less, string more) = GrassTooltips(setting);
            UiNode row = section.Row().Size(inner, height).Gap(UiTheme.ContainerSpacing).Main(StackMainAlignment.Start);
            readouts[(int)setting] = row.Text(
                    EditorGrassPanelSystem.Label(setting, new GrassPatch()),
                    UiTheme.SmallFontSize,
                    SceneUi.DimTextColor,
                    TextAlign.Left)
                .Size(inner - (height + UiTheme.ContainerSpacing) * 2, height);
            row.Button("-", 13, SceneUi.MutedButtonColor)
                .Size(height, height)
                .With(new EditorButton(EditorButtonKind.GrassStep, EditorGrassPanelSystem.StepArg(setting, -1)))
                .Tooltip(less);
            row.Button("+", 13, SceneUi.MutedButtonColor)
                .Size(height, height)
                .With(new EditorButton(EditorButtonKind.GrassStep, EditorGrassPanelSystem.StepArg(setting, 1)))
                .Tooltip(more);
        }

        UiNode color = section.ColorField(
            "BLADES",
            inner,
            "The grass's colour; each blade strays from it by the variation. Self modulate multiplies it, and a group's colour fade overrides it.");
        editor.AddComponent(new EditorGrassPanel(section.Entity, readouts, color.Entity));
    }

    private static (string Less, string More) GrassTooltips(GrassSetting setting) => setting switch
    {
        GrassSetting.Width => ("Narrower patch", "Wider patch"),
        GrassSetting.Length => ("Shorter blades", "Longer blades"),
        GrassSetting.Density => ("Fewer blades", "More blades"),
        GrassSetting.Sway => ("Less sway; at 0 the blades stand still", "More sway"),
        _ => ("Blades closer to the grass colour", "Blades stray further from the grass colour"),
    };

    /// <summary>The current tool's settings, docked to the viewport's top-left corner.</summary>
    private static void BuildLeftPanel(UiNode ui, Entity editor)
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

        UiNode snapSection = ToolSection(panel, inner, EditorToolSections.Snap);
        Entity snapLabel = SidebarLabel(snapSection, LocalizedText.Get(EditorStrings.Snap), inner, 14);
        Entity snap = BuildDropdown(
            snapSection,
            inner,
            EditorDropdownKind.Snap,
            new[] { "1/2 beat", "1/4 beat", "1/8 beat" },
            selected: 0);
        Playhead playhead = editor.GetComponent<Playhead>();
        SetDropdownSelected(snap, playhead.SnapDivision switch
        {
            8 => 1,
            16 => 2,
            _ => 0,
        });

        UiNode strengthSection = ToolSection(panel, inner, EditorToolSections.MoveStrength);
        SidebarLabel(strengthSection, "MOVE STRENGTH", inner, 12);
        const float strengthGap = 6;
        float strengthWidth = (inner - strengthGap * 2) / 3;
        UiNode strengths = strengthSection.Row().Size(inner, 28).Gap(strengthGap);
        for (int i = 0; i < 3; i++)
        {
            var strength = (DashStrength)i;
            strengths.Button(
                    i == 0 ? "1×" : i == 1 ? "2×" : "3×",
                    12,
                    SceneUi.MutedButtonColor)
                .Size(strengthWidth, 28)
                .With(EditorButton.Strength(strength))
                .Tooltip(strength switch
                {
                    DashStrength.Double => "Dashes carry twice as far; momentum notes push twice as hard",
                    DashStrength.Triple => "Dashes carry three times as far; momentum notes push three times as hard",
                    _ => "One dash length; one momentum push",
                });
        }

        BuildGrassPanel(panel, editor, inner);

        UiNode costSection = ToolSection(panel, inner, EditorToolSections.ContactCost);
        SidebarLabel(costSection, "CONTACT COST", inner, 12);
        const float costGap = 6;
        float costWidth = (inner - costGap * 3) / 4;
        UiNode costs = costSection.Row().Size(inner, 28).Gap(costGap);
        costs.Button("L−", 12, SceneUi.MutedButtonColor)
            .Size(costWidth, 28)
            .With(new EditorButton(EditorButtonKind.ContactLeft, -1))
            .Tooltip("Remove one left-side charge from new barriers");
        costs.Button("L+", 12, SceneUi.MutedButtonColor)
            .Size(costWidth, 28)
            .With(new EditorButton(EditorButtonKind.ContactLeft, 1))
            .Tooltip("Add one left-side charge to new barriers");
        costs.Button("R−", 12, SceneUi.MutedButtonColor)
            .Size(costWidth, 28)
            .With(new EditorButton(EditorButtonKind.ContactRight, -1))
            .Tooltip("Remove one right-side charge from new barriers");
        costs.Button("R+", 12, SceneUi.MutedButtonColor)
            .Size(costWidth, 28)
            .With(new EditorButton(EditorButtonKind.ContactRight, 1))
            .Tooltip("Add one right-side charge to new barriers");
        costSection.Text(
                "L1  R1",
                12,
                SceneUi.DimTextColor,
                TextAlign.Center)
            .Size(inner, 18)
            .With(new EditorReadout(EditorReadoutKind.ContactCost));

        UiNode shapeSection = ToolSection(panel, inner, EditorToolSections.FormShape);
        SidebarLabel(shapeSection, "FORM SHAPE", inner, 12);
        shapeSection.Button(
                "ATTACH SELECTED BLOCKS",
                10,
                SceneUi.MutedButtonColor)
            .Size(inner, 26)
            .With(new EditorButton(EditorButtonKind.ShapeAttach))
            .Tooltip("Attach the selected blocks to a new construction shape");
        shapeSection.Text(
                "NO SHAPE",
                12,
                SceneUi.DimTextColor,
                TextAlign.Center)
            .Size(inner, 18)
            .With(new EditorReadout(EditorReadoutKind.Shape));

        editor.AddComponent(new EditorLeftMenu(panel.Entity, snapLabel, snap));
    }

    private static UiNode ToolSection(UiNode panel, float width, EditorPlacementMode[] modes) =>
        panel.Column()
            .Width(width)
            .AutoHeight()
            .Gap(UiTheme.ContainerSpacing)
            .Main(StackMainAlignment.Start)
            .With(new EditorToolSection(modes));

    private static Entity SidebarLabel(
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
    private static void BuildNoteElements(UiNode ui, Entity editor)
    {
        const float swatchSize = 24;
        const float swatchGap = 5;
        const float wheelRadius = 92;
        float inner = KeybindSettings.Actions.Length * (swatchSize + swatchGap) - swatchGap;

        UiNode box = ui.Column()
            .Width(inner + PanelPadding * 2)
            .AutoHeight()
            .Gap(UiTheme.ContainerSpacing)
            .Fill(SceneUi.PanelColor)
            .Pad(PanelPadding)
            .Input()
            .Main(StackMainAlignment.Start)
            .Anchor(new Vector2(1, 0))
            .With(new EditorNoteBarPanel());

        SidebarLabel(box, "NOTE ELEMENTS", inner, 12);
        UiNode swatches = box.Row().Size(inner, swatchSize).Gap(swatchGap);
        foreach (KeybindAction action in KeybindSettings.Actions)
        {
            Color color = PlayfieldRenderer.ColorFor(action);
            swatches.Rect(color, size: new Vector2(swatchSize, swatchSize))
                .Input()
                .With(ButtonVisual.Create(color))
                .With(EditorButton.NoteType(action));
        }

        Entity label = SidebarLabel(box, "MOVE DIRECTION", inner, 12);
        UiWheelControl wheel = box.Wheel(
            Vector2.Zero,
            wheelRadius,
            DashDirectionWheel.Labels,
            selected: DashDirectionWheel.IndexOf(DashDirection.Up),
            hubRadius: 0.30f,
            gap: 0.045f);

        // A wheel opts out of its container by default; this one flows, so the box fits it.
        UiLayout.SetIgnored(wheel.Wheel, false);
        editor.AddComponent(new EditorDirectionWheel(wheel.Wheel, label));
    }

    private static Entity BuildDropdown(
        UiNode parent,
        float width,
        EditorDropdownKind kind,
        string[] labels,
        int selected)
    {
        UiNode button = parent.Button("", 13, SceneUi.EditorButtonColor).Size(width, 34);
        parent.DropdownList(
            button.Entity,
            labels,
            selected,
            optionColor: _ => SceneUi.MutedButtonColor);
        return button.With(new EditorDropdownControl(kind)).Entity;
    }

    private static void SetDropdownSelected(Entity button, int selected)
    {
        if (button.IsNull || !button.HasComponent<Dropdown>())
            return;

        ref Dropdown dropdown = ref button.GetComponent<Dropdown>();
        dropdown.Selected = selected;
        dropdown.Open = false;
    }
}

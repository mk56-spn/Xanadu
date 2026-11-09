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
            (UiNode button, Entity icon) = ToolButton(strip, size, group.Icon ?? group.Tools[0].Icon, group.Name, "◢", new EditorButton(EditorButtonKind.ToolGroup, index));
            button.With(new EditorToolGroup(index, flyout.Entity, icon));
        }
    }

    /// <summary>
    /// The shared XIF editor for the selected polygon, on a layer of its own just
    /// right of the tool strip, below the header. It hides while no polygon is selected.
    /// </summary>
    internal static void BuildXifPanel(EntityStore entities, Entity root, EditorShellRects shell)
    {
        UiNode layer = UiNode.Layer(entities, root, UiLayers.EditorXif, null, UiStage.Full);
        var home = new Vector2(
            shell.ToolStrip.End.X + EditorShellMetrics.ToolGap * 2 + XifEditorPanel.Width / 2,
            shell.ToolStrip.Position.Y + EditorShellMetrics.ToolGap * 2 + XifEditorPanel.Height / 2);
        XifEditorPanel.Build(layer, home, new EditorXifHost(entities));
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

        // Each part folds away under its header, so a long column stays short.
        panel.Collapsible("SELECTION", inner, out UiNode selection);
        UiNode modulate = selection.ColorField(
            "COLOUR",
            inner,
            "Self modulate of the selection: its colour is multiplied by this, and on a block this is the interior. Type #rrggbb or #rrggbbaa; leave blank for white. A group's colour fade overrides it.");
        editor.AddComponent(new EditorSelfModulateField(modulate.Entity));
        BuildBlockOutline(selection, editor, inner);
        BuildTextPanel(selection, editor, inner);
        BuildStartPanel(selection, inner);
        EditorSceneLayers.BuildInspector(selection, editor, inner);
        EditorSceneAlign.Build(selection, editor, inner);

        UiNode groups = panel.Collapsible("GROUPS", inner, out UiNode groupBody);
        EditorSceneGroups.BuildGroupGrid(groupBody, editor, inner, UiCollapsibleControl.HeaderOf(groups.Entity));

        editor.AddComponent(new EditorSidebar(shell.RightPanel));
        editor.AddComponent(new EditorSidebarLayout(panel.Entity));
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

        // Each setting is ruled off from the one before; the rules live in the
        // section so they hide with it.
        EditorSceneUi.Separator(section, inner);
        UiNode color = section.ColorField(
            "OUTLINE",
            inner,
            "The colour of the selected blocks' and spikes' outline. A group's colour fade overrides it.");

        EditorSceneUi.Separator(section, inner);

        // THICKNESS, then − value +, the value bright between its buttons.
        const float valueWidth = 44;
        UiNode row = section.Row().Size(inner, height).Gap(UiTheme.ContainerSpacing).Main(StackMainAlignment.Start);
        row.Text("THICKNESS", UiTheme.SmallFontSize, SceneUi.DimTextColor, TextAlign.Left)
            .Size(inner - height * 2 - valueWidth - UiTheme.ContainerSpacing * 3, height);
        row.Button("-", 13, SceneUi.MutedButtonColor)
            .Size(height, height)
            .With(new EditorButton(EditorButtonKind.OutlineStep, -1))
            .Tooltip("Thinner outline; at 0 there is none");
        Entity width = row.Text("0", 14, SceneUi.TextColor, TextAlign.Center)
            .Size(valueWidth, height)
            .Entity;
        row.Button("+", 13, SceneUi.MutedButtonColor)
            .Size(height, height)
            .With(new EditorButton(EditorButtonKind.OutlineStep, 1))
            .Tooltip("Thicker outline");

        editor.AddComponent(new EditorBlockOutlinePanel(section.Entity, width, color.Entity));
    }

    /// <summary>
    /// The selected start point's time and starting momentum; hidden until one
    /// start point is selected. <see cref="EditorMetadataSystem"/> shows it and
    /// keeps the fields in step with the start point.
    /// </summary>
    private static void BuildStartPanel(UiNode panel, float inner)
    {
        UiNode section = panel.Column()
            .Width(inner)
            .AutoHeight()
            .Gap(UiTheme.ContainerSpacing)
            .Main(StackMainAlignment.Start)
            .Hidden();

        EditorSceneUi.Separator(section, inner);
        StartField(section, inner, LocalizedText.Get(EditorStrings.StartTime), EditorFieldKind.StartTime);
        StartField(section, inner, LocalizedText.Get(EditorStrings.StartMomentumX), EditorFieldKind.StartMomentumX);
        StartField(section, inner, LocalizedText.Get(EditorStrings.StartMomentumY), EditorFieldKind.StartMomentumY);
        section.Entity.AddComponent(new EditorPanel(EditorPanelKind.Start, Vector2.Zero));
    }

    private static void StartField(UiNode section, float inner, string label, EditorFieldKind kind)
    {
        section.Text(label, UiTheme.SmallFontSize, SceneUi.DimTextColor, TextAlign.Left)
            .Size(inner, 18);
        section.Rect(Palette.SurfaceInset, "", new Vector2(inner, 28))
            .Input()
            .With(new TextStyle(13, UiTheme.Text))
            .Align(TextAlign.Left)
            .With(new TextField("", true))
            .With(new EditorField(kind));
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

        EditorSceneUi.Separator(section, inner);
        section.Text("TEXT", UiTheme.SmallFontSize, SceneUi.DimTextColor, TextAlign.Left)
            .Size(inner, 18);
        Entity field = section.Rect(Palette.SurfaceInset, LevelText.Placeholder, new Vector2(inner, height))
            .Input()
            .With(new TextStyle(13, UiTheme.Text))
            .Align(TextAlign.Left)
            .With(new TextField(LevelText.Placeholder))
            .Tooltip("The selected text's words; click anywhere else to finish")
            .Entity;

        EditorSceneUi.Separator(section, inner);
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
        UiNode section = EditorSceneUi.ToolSection(panel, inner, EditorToolSections.Grass);
        EditorSceneUi.SidebarLabel(section, "GRASS", inner, 12);

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

    /// <summary>
    /// The block tool's sizes, side by side: the quarter block, a cell, and the
    /// platform. Each button draws the block it places, to scale with the others.
    /// </summary>
    private static void BuildBlockSize(UiNode panel, float inner)
    {
        UiNode section = EditorSceneUi.ToolSection(panel, inner, EditorToolSections.BlockSize);
        EditorSceneUi.SidebarLabel(section, "BLOCK SIZE", inner, 12);

        const float sizeGap = 6;
        const float buttonHeight = 32;
        float sizeWidth = (inner - sizeGap * 2) / 3;
        UiNode sizes = section.Row().Size(inner, buttonHeight).Gap(sizeGap);
        foreach (BlockBrushSize size in new[] { BlockBrushSize.Quarter, BlockBrushSize.Cell, BlockBrushSize.Platform })
        {
            UiNode button = sizes.Row()
                .Size(sizeWidth, buttonHeight)
                .Fill(SceneUi.MutedButtonColor)
                .Input()
                .Main(StackMainAlignment.Center)
                .Cross(StackCrossAlignment.Center)
                .With(ButtonVisual.Create(SceneUi.MutedButtonColor))
                .With(new EditorButton(EditorButtonKind.BlockSize, (int)size))
                .Tooltip(size switch
                {
                    BlockBrushSize.Quarter => "A quarter block: half a cell wide and half a cell high",
                    BlockBrushSize.Platform => "A platform: two cells wide and one high",
                    _ => "A block one cell square; the default",
                });
            button.Rect(PlayfieldRenderer.BlockFill, size: BlockPreviewSize(size));
        }
    }

    /// <summary>A block's preview in the size buttons: a cell is 14 px, the rest to scale.</summary>
    private static Vector2 BlockPreviewSize(BlockBrushSize size) => size switch
    {
        BlockBrushSize.Quarter => new Vector2(7, 7),
        BlockBrushSize.Platform => new Vector2(28, 14),
        _ => new Vector2(14, 14),
    };

    private static (string Less, string More) GrassTooltips(GrassSetting setting) => setting switch
    {
        GrassSetting.Width => ("Narrower patch", "Wider patch"),
        GrassSetting.Length => ("Shorter blades", "Longer blades"),
        GrassSetting.Density => ("Fewer blades", "More blades"),
        GrassSetting.Sway => ("Less sway; at 0 the blades stand still", "More sway"),
        _ => ("Blades closer to the grass colour", "Blades stray further from the grass colour"),
    };

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

        BuildBlockSize(panel, inner);

        UiNode strengthSection = EditorSceneUi.ToolSection(panel, inner, EditorToolSections.MoveStrength);
        EditorSceneUi.SidebarLabel(strengthSection, "MOVE STRENGTH", inner, 12);
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

        UiNode diagonalSection = EditorSceneUi.ToolSection(panel, inner, EditorToolSections.Diagonal);
        EditorSceneUi.SidebarLabel(diagonalSection, "DIAGONAL", inner, 12);
        UiNode diagonalRow = diagonalSection.Row().Size(inner, 28).Gap(6);
        diagonalRow.Text("RHOMBUS: OFF", 12, SceneUi.DimTextColor, TextAlign.Left)
            .Size(inner - 86, 28)
            .With(new EditorReadout(EditorReadoutKind.Diagonal));
        diagonalRow.Button("TOGGLE", 12, SceneUi.MutedButtonColor)
            .Size(80, 28)
            .With(new EditorButton(EditorButtonKind.Diagonal))
            .Tooltip("A rhombus note turns the pressed way 45° clockwise: up becomes up-right");

        BuildGrassPanel(panel, editor, inner);
        EditorSceneFlarePanel.Build(panel, editor, inner);

        // One stepper row per bar, in its action key's colour: at most 4 a bar,
        // and at most two bars a barrier.
        UiNode costSection = EditorSceneUi.ToolSection(panel, inner, EditorToolSections.ContactCost);
        EditorSceneUi.SidebarLabel(costSection, "CONTACT COST", inner, 12);
        EditorReadoutKind[] readouts = { EditorReadoutKind.ContactFirst, EditorReadoutKind.ContactSecond, EditorReadoutKind.ContactThird };
        const float stepWidth = 36;
        for (int bar = 0; bar < ContactCounters.Bars; bar++)
        {
            UiNode row = costSection.Row().Size(inner, 28).Gap(6);
            row.Text($"BAR {bar + 1}: 0", 12, PlayfieldRenderer.BarColor(bar), TextAlign.Left)
                .Size(inner - stepWidth * 2 - 12, 28)
                .With(new EditorReadout(readouts[bar]));
            row.Button("−", 12, SceneUi.MutedButtonColor)
                .Size(stepWidth, 28)
                .With(EditorButton.ContactCost(bar, -1))
                .Tooltip("Ask one less of this bar");
            row.Button("+", 12, SceneUi.MutedButtonColor)
                .Size(stepWidth, 28)
                .With(EditorButton.ContactCost(bar, 1))
                .Tooltip("Ask one more of this bar (at most 4, and two bars a barrier)");
        }

        editor.AddComponent(new EditorLeftMenu(panel.Entity));
    }

    /// <summary>How far the note elements box sits in from the viewport's top-right corner.</summary>
    private const float NoteElementsMargin = 10;

    private const float NoteElementsBorder = 2;

    internal static void BuildNoteElements(UiNode ui, Entity editor)
    {
        const float swatchSize = 36;
        const float swatchGap = 8;
        float inner = KeybindSettings.ActionKeys.Length * (swatchSize + swatchGap) - swatchGap;

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
            .Border(PlayfieldRenderer.ColorFor(KeybindSettings.ActionKeys[0]), NoteElementsBorder)
            .With(new EditorNoteBarPanel());

        EditorSceneUi.SidebarLabel(box, "ACTION KEY", inner, 12);
        UiNode swatches = box.Row().Size(inner, swatchSize).Gap(swatchGap);
        foreach (KeybindAction action in KeybindSettings.ActionKeys)
        {
            Color color = PlayfieldRenderer.ColorFor(action);
            swatches.Rect(color, size: new Vector2(swatchSize, swatchSize))
                .Input()
                .With(ButtonVisual.Create(color))
                .With(EditorButton.NoteType(action));
        }
    }
}

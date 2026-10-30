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

namespace Xanadu.Scenes;

/// <summary>Creates the initial editor state and a playfield centered between the header and footer.</summary>
public static partial class EditorScene
{
    private const float PanelPadding = UiTheme.ContainerPadding;
    private const float LeftPanelWidth = 210;

    /// <summary>The placement tools run down the shell's tool strip at the left edge.</summary>
    private static void BuildToolStrip(UiNode ui, EditorShellRects shell)
    {
        UiNode strip = ui.Column()
            .Size(shell.ToolStrip.Size)
            .Gap(EditorShellMetrics.ToolGap)
            .Fill(SceneUi.PanelColor)
            .Pad((EditorShellMetrics.ToolStripWidth - EditorShellMetrics.ToolButton) / 2)
            .Input()
            .Main(StackMainAlignment.Start);

        var size = new Vector2(
            EditorShellMetrics.ToolButton,
            EditorShellMetrics.ToolButton);
        foreach ((EditorPlacementMode mode, string icon) in ToolIcons)
        {
            strip.Button(icon, 26, SceneUi.MutedButtonColor)
                .Size(size)
                .With(new EditorPlacementButton(mode));
        }
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
        BuildGroupGrid(panel, editor, inner);

        editor.AddComponent(new EditorSidebar(shell.RightPanel));
        editor.AddComponent(new EditorSidebarLayout(
            panel.Entity,
            characterLabel,
            character));
    }

    /// <summary>Snap and dash strength, docked to the viewport's top-left corner.</summary>
    private static void BuildLeftPanel(UiNode ui, Entity editor)
    {
        float inner = LeftPanelWidth - PanelPadding * 2;
        UiNode panel = ui.Column()
            .Width(LeftPanelWidth)
            .AutoHeight()
            .Gap(UiTheme.ContainerSpacing)
            .Fill(SceneUi.PanelColor)
            .Pad(PanelPadding)
            .Main(StackMainAlignment.Start)
            .Anchor(Vector2.Zero);

        Entity snapLabel = SidebarLabel(panel, LocalizedText.Get(EditorStrings.Snap), inner, 14);
        Entity snap = BuildDropdown(
            panel,
            inner,
            EditorDropdownKind.Snap,
            new[] { "1/4 beat", "1/8 beat", "1/16 beat" },
            selected: 0);
        Playhead playhead = editor.GetComponent<Playhead>();
        SetDropdownSelected(snap, playhead.SnapDivision switch
        {
            8 => 1,
            16 => 2,
            _ => 0,
        });

        SidebarLabel(panel, "MOVE STRENGTH", inner, 12);
        const float strengthGap = 6;
        float strengthWidth = (inner - strengthGap * 2) / 3;
        UiNode strengths = panel.Row().Size(inner, 28).Gap(strengthGap);
        for (int i = 0; i < 3; i++)
        {
            var strength = (DashStrength)i;
            strengths.Button(
                    i == 0 ? "1×" : i == 1 ? "2×" : "3×",
                    12,
                    SceneUi.MutedButtonColor)
                .Size(strengthWidth, 28)
                .With(new EditorDashStrengthButton(strength))
                .Tooltip(strength switch
                {
                    DashStrength.Double => "Dashes carry twice as far; momentum notes push twice as hard",
                    DashStrength.Triple => "Dashes carry three times as far; momentum notes push three times as hard",
                    _ => "One dash length; one momentum push",
                });
        }

        SidebarLabel(panel, "CONTACT COST", inner, 12);
        const float costGap = 6;
        float costWidth = (inner - costGap * 3) / 4;
        UiNode costs = panel.Row().Size(inner, 28).Gap(costGap);
        costs.Button("L−", 12, SceneUi.MutedButtonColor)
            .Size(costWidth, 28)
            .With(new EditorContactCostButton(left: true, delta: -1))
            .Tooltip("Remove one left-side charge from new barriers");
        costs.Button("L+", 12, SceneUi.MutedButtonColor)
            .Size(costWidth, 28)
            .With(new EditorContactCostButton(left: true, delta: 1))
            .Tooltip("Add one left-side charge to new barriers");
        costs.Button("R−", 12, SceneUi.MutedButtonColor)
            .Size(costWidth, 28)
            .With(new EditorContactCostButton(left: false, delta: -1))
            .Tooltip("Remove one right-side charge from new barriers");
        costs.Button("R+", 12, SceneUi.MutedButtonColor)
            .Size(costWidth, 28)
            .With(new EditorContactCostButton(left: false, delta: 1))
            .Tooltip("Add one right-side charge to new barriers");
        Entity costReadout = panel.Text(
                "L1  R1",
                12,
                SceneUi.DimTextColor,
                TextAlign.Center)
            .Size(inner, 18);
        editor.AddComponent(new EditorContactCostState(1, 1));
        editor.AddComponent(new EditorContactCostReadout(costReadout));

        SidebarLabel(panel, "FORM SHAPE", inner, 12);
        panel.Button(
                "ATTACH SELECTED BLOCKS",
                10,
                SceneUi.MutedButtonColor)
            .Size(inner, 26)
            .With(new EditorShapeAttachButton())
            .Tooltip("Attach the selected blocks to a new construction shape");
        Entity shapeReadout = panel.Text(
                "NO SHAPE",
                12,
                SceneUi.DimTextColor,
                TextAlign.Center)
            .Size(inner, 18);
        editor.AddComponent(new EditorShapeState(0));
        editor.AddComponent(new EditorShapeReadout(shapeReadout));

        BuildNoteKeyBar(panel, inner);

        editor.AddComponent(new EditorLeftMenu(panel.Entity, snapLabel, snap));
    }

    private static Entity SidebarLabel(
        UiNode parent,
        string text,
        float width,
        int fontSize) =>
        parent.Text(text, fontSize, SceneUi.DimTextColor, TextAlign.Center)
            .Size(width, fontSize + 10);

    /// <summary>
    /// The dash direction wheel floats beside the sidebar: it only shows for
    /// dash notes and the editor moves it on and off screen.
    /// </summary>
    private static void BuildDirectionWheelOverlay(
        EntityStore entities,
        Entity root,
        Entity editor,
        EditorShellRects shell)
    {
        UiNode ui = UiNode.Layer(entities, root, 941, null, UiStage.Full);
        Vector2 home = new(
            shell.RightPanel.Position.X - 125,
            shell.RightPanel.Position.Y + 265);
        (UiWheelControl wheel, Entity label) = BuildDirectionWheel(ui, home);
        editor.AddComponent(new EditorDirectionWheel(wheel.Wheel, label, home));
    }

    private static (UiWheelControl Wheel, Entity Label) BuildDirectionWheel(
        UiNode ui,
        Vector2 home)
    {
        UiWheelControl wheel = ui.Wheel(
            home,
            92,
            DashDirectionWheel.Labels,
            selected: DashDirectionWheel.IndexOf(DashDirection.Up),
            hubRadius: 0.30f,
            gap: 0.045f);
        Entity label = ui.Text(
                "MOVE DIRECTION",
                12,
                SceneUi.DimTextColor,
                TextAlign.Center)
            .Size(190, 18)
            .At(home + new Vector2(0, -92 - 16));
        return (wheel, label);
    }

    /// <summary>Icons for the placement tools, in bar order; two letters where no symbol fits.</summary>
    private static readonly (EditorPlacementMode Mode, string Icon)[] ToolIcons =
    {
        (EditorPlacementMode.Notes, "🎵"),
        (EditorPlacementMode.Pair, "P"),
        (EditorPlacementMode.Dash, "➜"),
        (EditorPlacementMode.Momentum, "M"),
        (EditorPlacementMode.Mines, "✹"),
        (EditorPlacementMode.Start, "⚑"),
        (EditorPlacementMode.Contact, "◎"),
        (EditorPlacementMode.CharacterSwitch, "⇄"),
        (EditorPlacementMode.WeaponSwitch, "W"),
        (EditorPlacementMode.Channel, "◑"),
        (EditorPlacementMode.FormStep, "F"),
        (EditorPlacementMode.Blocks, "■"),
        (EditorPlacementMode.Spikes, "▲"),
    };

    /// <summary>
    /// The note keys new notes get, as a row of colour swatches at the foot of
    /// the left panel. Normal notes take a specific key and pair notes take
    /// their number from the selected key, so the bar shows in both modes;
    /// elsewhere it leaves the panel and the panel shrinks.
    /// </summary>
    private static void BuildNoteKeyBar(UiNode parent, float width)
    {
        const float swatchSize = 24;
        const float swatchGap = 5;

        UiNode bar = parent.Column()
            .Width(width)
            .AutoHeight()
            .Gap(UiTheme.ContainerSpacing);
        bar.With(new EditorNoteBarPanel());

        SidebarLabel(bar, "NOTE KEY", width, 12);
        UiNode swatches = bar.Row().Size(width, swatchSize).Gap(swatchGap);
        foreach (KeybindAction action in KeybindSettings.Actions)
        {
            Color color = PlayfieldRenderer.ColorFor(action);
            swatches.Rect(color, size: new Vector2(swatchSize, swatchSize))
                .Input()
                .With(ButtonVisual.Create(color))
                .With(new EditorNoteSwatch(action));
        }
    }

    private static Entity BuildDropdown(
        UiNode parent,
        float width,
        EditorDropdownKind kind,
        string[] labels,
        int selected)
    {
        UiNode button = parent.Button("", 13, SceneUi.ButtonColor).Size(width, 34);
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

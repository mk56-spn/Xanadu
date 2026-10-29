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
    private static void BuildToolStrip(UiBuilder ui, EditorShellRects shell)
    {
        ui.Container(
            Vector2.Zero,
            shell.ToolStrip.Size,
            StackDirection.Vertical,
            spacing: EditorShellMetrics.ToolGap,
            color: SceneUi.PanelColor,
            padding: (EditorShellMetrics.ToolStripWidth - EditorShellMetrics.ToolButton) / 2,
            takesInput: true,
            main: StackMainAlignment.Start);
        var size = new Vector2(EditorShellMetrics.ToolButton, EditorShellMetrics.ToolButton);
        foreach ((EditorPlacementMode mode, string icon) in ToolIcons)
        {
            Entity button = ui.Button(icon, size, SceneUi.MutedButtonColor, fontSize: 26);
            button.AddComponent(new EditorPlacementButton(mode));
        }
        ui.End();
    }

    /// <summary>
    /// The shell's right panel, docked to the right edge for the full height of
    /// the middle row. Its controls flow top to bottom, so an open dropdown
    /// pushes the groups down by itself.
    /// </summary>
    private static void BuildSidebar(UiBuilder ui, Entity editor, EditorShellRects shell)
    {
        float inner = shell.RightPanel.Size.X - PanelPadding * 2;
        ui.Container(
            Vector2.Zero,
            shell.RightPanel.Size,
            StackDirection.Vertical,
            spacing: UiTheme.ContainerSpacing,
            color: SceneUi.PanelColor,
            padding: PanelPadding,
            takesInput: true,
            main: StackMainAlignment.Start);
        Entity panel = ui.Current;
        panel.AddTag<LayoutIgnore>();
        panel.AddComponent(new UiAnchor(new Vector2(1, 0), new Vector2(1, 0)));

        Entity characterLabel = SidebarLabel(ui, "CHARACTER", inner, 14);
        Entity character = BuildDropdown(
            ui,
            inner,
            EditorDropdownKind.Character,
            new[] { "CONTACT" },
            selected: 0);
        BuildGroupGrid(ui, editor, inner);
        ui.End();

        editor.AddComponent(new EditorSidebar(shell.RightPanel));
        editor.AddComponent(new EditorSidebarLayout(panel, characterLabel, character));
    }

    /// <summary>Snap and dash strength, docked to the viewport's top-left corner.</summary>
    private static void BuildLeftPanel(UiBuilder ui, Entity editor)
    {
        float inner = LeftPanelWidth - PanelPadding * 2;
        ui.Container(
            Vector2.Zero,
            new Vector2(LeftPanelWidth, 4),
            StackDirection.Vertical,
            spacing: UiTheme.ContainerSpacing,
            color: SceneUi.PanelColor,
            padding: PanelPadding,
            autoHeight: true,
            main: StackMainAlignment.Start);
        Entity panel = ui.Current;
        panel.AddTag<LayoutIgnore>();
        panel.AddComponent(new UiAnchor(Vector2.Zero, Vector2.Zero));

        Entity snapLabel = SidebarLabel(ui, LocalizedText.Get(EditorStrings.Snap), inner, 14);
        Entity snap = BuildDropdown(
            ui,
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

        SidebarLabel(ui, "DASH STRENGTH", inner, 12);
        const float strengthGap = 6;
        float strengthWidth = (inner - strengthGap * 2) / 3;
        ui.Container(
            Vector2.Zero,
            new Vector2(inner, 28),
            StackDirection.Horizontal,
            spacing: strengthGap,
            color: Clear);
        for (int i = 0; i < 3; i++)
        {
            var strength = (DashStrength)i;
            Entity button = ui.Button(
                i == 0 ? "1×" : i == 1 ? "2×" : "3×",
                new Vector2(strengthWidth, 28),
                SceneUi.MutedButtonColor,
                fontSize: 12);
            button.AddComponent(new EditorDashStrengthButton(strength));
            button.AddComponent(new UiTooltip(strength switch
            {
                DashStrength.Double => "Dash notes carry the player twice as far",
                DashStrength.Triple => "Dash notes carry the player three times as far",
                _ => "Dash notes carry the player one dash length",
            }));
        }
        ui.End();

        BuildNoteKeyBar(ui, inner);
        ui.End();

        editor.AddComponent(new EditorLeftMenu(panel, snapLabel, snap));
    }

    private static Entity SidebarLabel(UiBuilder ui, string text, float width, int fontSize) =>
        SceneUi.Text(
            ui,
            Vector2.Zero,
            new Vector2(width, fontSize + 10),
            text,
            fontSize,
            SceneUi.DimTextColor,
            Clear);

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
        UiBuilder ui = SceneUi.Layer(entities, root, 941);
        Vector2 home = new(
            shell.RightPanel.Position.X - 125,
            shell.RightPanel.Position.Y + 265);
        (UiWheelControl wheel, Entity label) = BuildDirectionWheel(ui, home);
        editor.AddComponent(new EditorDirectionWheel(wheel.Wheel, label, home));
    }

    private static (UiWheelControl Wheel, Entity Label) BuildDirectionWheel(
        UiBuilder ui,
        Vector2 home)
    {
        UiWheelControl wheel = ui.Wheel(
            home,
            92,
            DashDirectionWheel.Labels,
            selected: DashDirectionWheel.IndexOf(DashDirection.Up),
            hubRadius: 0.30f,
            gap: 0.045f);
        Entity label = SceneUi.Text(
            ui,
            home + new Vector2(0, -92 - 16),
            new Vector2(190, 18),
            "DASH DIRECTION",
            12,
            SceneUi.DimTextColor,
            new Color(0, 0, 0, 0));
        return (wheel, label);
    }

    /// <summary>Icons for the placement tools, in bar order; two letters where no symbol fits.</summary>
    private static readonly (EditorPlacementMode Mode, string Icon)[] ToolIcons =
    {
        (EditorPlacementMode.Notes, "🎵"),
        (EditorPlacementMode.Mines, "✹"),
        (EditorPlacementMode.Movement, "➜"),
        (EditorPlacementMode.Start, "⚑"),
        (EditorPlacementMode.Contact, "◎"),
        (EditorPlacementMode.CharacterSwitch, "⇄"),
        (EditorPlacementMode.Blocks, "■"),
        (EditorPlacementMode.Spikes, "▲"),
    };

    /// <summary>
    /// The note keys new notes get, as a row of colour swatches at the foot of
    /// the left panel. Notes and dash notes both carry a key, so the bar shows in
    /// both modes; elsewhere it leaves the panel and the panel shrinks.
    /// </summary>
    private static void BuildNoteKeyBar(UiBuilder ui, float width)
    {
        const float swatchSize = 24;
        const float swatchGap = 5;
        ui.Container(
            Vector2.Zero,
            new Vector2(width, 4),
            StackDirection.Vertical,
            spacing: UiTheme.ContainerSpacing,
            color: Clear,
            autoHeight: true);
        ui.Current.AddComponent(new EditorNoteBarPanel());
        SidebarLabel(ui, "NOTE KEY", width, 12);
        ui.Container(
            Vector2.Zero,
            new Vector2(width, swatchSize),
            StackDirection.Horizontal,
            spacing: swatchGap,
            color: Clear);
        foreach (KeybindAction action in KeybindSettings.Actions)
        {
            Color color = PlayfieldRenderer.ColorFor(action);
            Entity swatch = ui.RectEntity(
                Vector2.Zero,
                new Vector2(swatchSize, swatchSize),
                color,
                takesInput: true);
            swatch.AddComponent(ButtonVisual.Create(color));
            swatch.AddComponent(new EditorNoteSwatch(action));
        }
        ui.End();
        ui.End();
    }

    private static Entity BuildDropdown(
        UiBuilder ui,
        float width,
        EditorDropdownKind kind,
        string[] labels,
        int selected)
    {
        Entity button = ui.Button("", new Vector2(width, 34), SceneUi.ButtonColor, fontSize: 13);
        ui.DropdownList(button, labels, selected, optionColor: _ => SceneUi.MutedButtonColor);
        button.AddComponent(new EditorDropdownControl(kind));
        return button;
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

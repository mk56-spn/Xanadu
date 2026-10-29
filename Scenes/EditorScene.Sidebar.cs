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
    private static void BuildSidebar(
        EntityStore entities,
        Entity root,
        Entity editor,
        Vector2 screen)
    {
        // The shell's right panel: flush to the screen edge, under the header.
        const float width = EditorShellMetrics.RightPanelWidth - 24;
        const float top = EditorShellMetrics.HeaderHeight + 10;
        const float SidebarHeight = 88 + GroupGridHeight;
        float centerX = screen.X - EditorShellMetrics.RightPanelWidth / 2;

        UiBuilder panelUi = SceneUi.Layer(entities, root, 939);
        Entity sidebarPanel = panelUi.RectEntity(
            new Vector2(centerX, top - 10 + SidebarHeight / 2),
            new Vector2(width + 24, SidebarHeight),
            SceneUi.PanelColor,
            takesInput: true);

        UiBuilder ui = SceneUi.Layer(entities, root, 940);

        editor.AddComponent(new EditorSidebar(new Rect2(
            centerX - width / 2 - 12,
            top - 10,
            width + 24,
            SidebarHeight)));

        Entity characterLabel = SceneUi.Text(
            ui,
            new Vector2(centerX, top + 12),
            new Vector2(width, 24),
            "CHARACTER",
            14,
            SceneUi.DimTextColor,
            new Color(0, 0, 0, 0));
        Entity character = BuildDropdown(
            ui,
            centerX,
            top + 41,
            width,
            EditorDropdownKind.Character,
            new[] { "CONTACT" },
            selected: 0);

        BuildGroupGrid(ui, editor, centerX, top);
        Vector2 wheelHome = new(centerX - 250, top + 255);
        (UiWheelControl wheel, Entity wheelLabel) =
            BuildDirectionWheel(ui, wheelHome);
        editor.AddComponent(new EditorDirectionWheel(
            wheel.Wheel,
            wheelLabel,
            wheelHome));
        editor.AddComponent(new EditorSidebarLayout(
            sidebarPanel,
            characterLabel,
            character,
            top));
    }

    private static void BuildLeftMenu(
        EntityStore entities,
        Entity root,
        Entity editor,
        Vector2 screen)
    {
        // Clear of the tool strip at the left edge.
        const float width = 190;
        const float top = EditorShellMetrics.HeaderHeight + 10;
        const float centerX = EditorShellMetrics.ToolStripWidth + 8 + (width + 20) / 2;

        UiBuilder panelUi = SceneUi.Layer(entities, root, 939);
        Entity panel = panelUi.RectEntity(
            new Vector2(centerX, top + 115),
            new Vector2(width + 20, 230),
            SceneUi.PanelColor,
            takesInput: false);

        UiBuilder ui = SceneUi.Layer(entities, root, 941);
        Entity snapLabel = SceneUi.Text(
            ui,
            new Vector2(centerX, top + 12),
            new Vector2(width, 24),
            LocalizedText.Get(EditorStrings.Snap),
            14,
            SceneUi.DimTextColor,
            new Color(0, 0, 0, 0));
        Entity snap = BuildDropdown(
            ui,
            centerX,
            top + 41,
            width,
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

        SceneUi.Text(
            ui,
            new Vector2(centerX, top + 80),
            new Vector2(width, 20),
            "DASH STRENGTH",
            12,
            SceneUi.DimTextColor,
            new Color(0, 0, 0, 0));
        const float strengthWidth = 54;
        const float strengthGap = 6;
        const float strengthTotal = strengthWidth * 3 + strengthGap * 2;
        float strengthLeft = centerX - strengthTotal / 2 + strengthWidth / 2;
        for (int i = 0; i < 3; i++)
        {
            var strength = (DashStrength)i;
            Entity button = SceneUi.ButtonEntity(
                ui,
                new Vector2(
                    strengthLeft + i * (strengthWidth + strengthGap),
                    top + 112),
                new Vector2(strengthWidth, 28),
                i == 0 ? "1×" : i == 1 ? "2×" : "3×",
                12,
                SceneUi.MutedButtonColor);
            button.AddComponent(new EditorDashStrengthButton(strength));
            button.AddComponent(new UiTooltip(strength switch
            {
                DashStrength.Double => "Dash notes carry the player twice as far",
                DashStrength.Triple => "Dash notes carry the player three times as far",
                _ => "Dash notes carry the player one dash length",
            }));
        }

        editor.AddComponent(new EditorLeftMenu(
            panel,
            snapLabel,
            snap,
            top));
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

    /// <summary>The placement tools run down the shell's tool strip at the left edge.</summary>
    private static void BuildToolBar(EntityStore entities, Entity root, Vector2 screen)
    {
        const float buttonSize = EditorShellMetrics.ToolButton;
        const float step = EditorShellMetrics.ToolButton + EditorShellMetrics.ToolGap;
        const float centerX = EditorShellMetrics.ToolStripWidth / 2;
        const float top = EditorShellMetrics.HeaderHeight;
        float height = screen.Y - top - EditorFooterHeight.Level;

        SceneUi.Layer(entities, root, 939).RectEntity(
            new Vector2(centerX, top + height / 2),
            new Vector2(EditorShellMetrics.ToolStripWidth, height),
            SceneUi.PanelColor,
            takesInput: true);

        UiBuilder ui = SceneUi.Layer(entities, root, 941);
        for (int i = 0; i < ToolIcons.Length; i++)
        {
            Entity button = ui.RectEntity(
                new Vector2(
                    centerX,
                    top + EditorShellMetrics.ToolGap + buttonSize / 2 + i * step),
                new Vector2(buttonSize, buttonSize),
                SceneUi.MutedButtonColor,
                ToolIcons[i].Icon,
                takesInput: true);
            button.AddComponent(new TextStyle(26, SceneUi.TextColor));
            button.AddComponent(ButtonVisual.Create(SceneUi.MutedButtonColor));
            button.AddComponent(new EditorPlacementButton(ToolIcons[i].Mode));
        }
    }

    /// <summary>The horizontal bar of note colours, shown only in note mode.</summary>
    private static void BuildNoteTypeBar(EntityStore entities, Entity root)
    {
        const float swatchSize = 24;
        const float step = 29;
        const float centerX = EditorShellMetrics.ToolStripWidth + 8 + 105;
        const float centerY = 380;
        float width = KeybindSettings.Actions.Length * step + 8;

        Vector2 panelHome = new(centerX, centerY);
        Entity panel = SceneUi.Layer(entities, root, 939).RectEntity(
            UiLayout.Hidden,
            new Vector2(width, swatchSize + 12),
            SceneUi.PanelColor,
            takesInput: true);
        panel.AddComponent(new EditorNoteBarPanel(panelHome));

        UiBuilder ui = SceneUi.Layer(entities, root, 941);
        float left = centerX - KeybindSettings.Actions.Length * step / 2 + step / 2;
        for (int i = 0; i < KeybindSettings.Actions.Length; i++)
        {
            KeybindAction action = KeybindSettings.Actions[i];
            Color color = PlayfieldRenderer.ColorFor(action);
            Entity swatch = ui.RectEntity(
                UiLayout.Hidden,
                new Vector2(swatchSize, swatchSize),
                color,
                takesInput: true);
            swatch.AddComponent(ButtonVisual.Create(color));
            swatch.AddComponent(new EditorNoteSwatch(
                action,
                new Vector2(left + i * step, centerY)));
        }
    }

    private static Entity BuildDropdown(
        UiBuilder ui,
        float centerX,
        float centerY,
        float width,
        EditorDropdownKind kind,
        string[] labels,
        int selected)
    {
        Entity button = ui.RectEntity(
            new Vector2(centerX, centerY),
            new Vector2(width, 34),
            SceneUi.ButtonColor,
            "",
            takesInput: true);
        button.AddComponent(new TextStyle(13, SceneUi.TextColor));
        button.AddComponent(ButtonVisual.Create(SceneUi.ButtonColor));

        float listHeight = labels.Length * 30 + 6;
        Entity list = ui.RectEntity(
            UiLayout.Hidden,
            Vector2.Zero,
            new Color(0, 0, 0, 0),
            takesInput: true);

        for (int i = 0; i < labels.Length; i++)
        {
            Entity option = ui.RectEntity(
                UiLayout.Hidden,
                new Vector2(width - 6, 28),
                SceneUi.MutedButtonColor,
                labels[i],
                takesInput: true);
            option.AddComponent(new TextStyle(13, SceneUi.TextColor));
            option.AddComponent(ButtonVisual.Create(SceneUi.MutedButtonColor));
            option.AddComponent(new DropdownOption(button, i));
        }

        button.AddComponent(new Dropdown(
            list,
            selected,
            new Vector2(width, listHeight)));
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

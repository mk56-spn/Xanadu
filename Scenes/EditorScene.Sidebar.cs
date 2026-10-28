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
        const float width = 190;
        const float top = 120;
        const float SidebarHeight = 88 + GroupGridHeight;
        float centerX = screen.X - 112;

        UiBuilder panelUi = SceneUi.Layer(entities, root, 939);
        Entity sidebarPanel = panelUi.RectEntity(
            new Vector2(centerX, top - 10 + SidebarHeight / 2),
            new Vector2(width + 20, SidebarHeight),
            SceneUi.PanelColor,
            takesInput: true);

        UiBuilder ui = SceneUi.Layer(entities, root, 940);

        editor.AddComponent(new EditorSidebar(new Rect2(
            centerX - width / 2 - 10,
            top - 10,
            width + 20,
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
        const float width = 190;
        const float top = 120;
        const float centerX = 164;

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
    };

    /// <summary>The vertical bar of placement tools down the left edge.</summary>
    private static void BuildToolBar(EntityStore entities, Entity root)
    {
        const float buttonSize = 36;
        const float step = 42;
        const float centerX = 30;
        const float top = 120;
        float height = ToolIcons.Length * step + 6;

        SceneUi.Layer(entities, root, 939).RectEntity(
            new Vector2(centerX, top + height / 2 - 3),
            new Vector2(buttonSize + 8, height),
            SceneUi.PanelColor,
            takesInput: true);

        UiBuilder ui = SceneUi.Layer(entities, root, 941);
        for (int i = 0; i < ToolIcons.Length; i++)
        {
            Entity button = ui.RectEntity(
                new Vector2(centerX, top + buttonSize / 2 + i * step),
                new Vector2(buttonSize, buttonSize),
                SceneUi.MutedButtonColor,
                ToolIcons[i].Icon,
                takesInput: true);
            button.AddComponent(new TextStyle(22, SceneUi.TextColor));
            button.AddComponent(ButtonVisual.Create(SceneUi.MutedButtonColor));
            button.AddComponent(new EditorPlacementButton(ToolIcons[i].Mode));
        }
    }

    /// <summary>The horizontal bar of note colours, shown only in note mode.</summary>
    private static void BuildNoteTypeBar(EntityStore entities, Entity root)
    {
        const float swatchSize = 24;
        const float step = 29;
        const float centerX = 164;
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

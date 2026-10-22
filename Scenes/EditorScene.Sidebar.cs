using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Position = Writer.Ecs.Position;
using Writer.Levels;
using Writer.Settings;
using Writer.Editor;
using Writer.Systems.Editor;
using Writer.Strings;
using Writer.Systems.Playfields;

namespace Writer.Scenes;

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
        float centerX = screen.X - 112;

        UiBuilder panelUi = SceneUi.Layer(entities, root, 939);
        Entity sidebarPanel = panelUi.RectEntity(
            new Vector2(centerX, top + 141),
            new Vector2(width + 20, 282),
            SceneUi.PanelColor,
            takesInput: true);

        UiBuilder ui = SceneUi.Layer(entities, root, 940);

        editor.AddComponent(new EditorSidebar(new Rect2(
            centerX - width / 2 - 10,
            top - 10,
            width + 20,
            282)));

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

        Entity typeLabel = SceneUi.Text(
            ui,
            new Vector2(centerX, top + 86),
            new Vector2(width, 24),
            LocalizedText.Get(EditorStrings.NoteType),
            14,
            SceneUi.DimTextColor,
            new Color(0, 0, 0, 0));

        var labels = new string[KeybindSettings.Actions.Length];
        for (int i = 0; i < labels.Length; i++)
            labels[i] = KeybindSettings.LabelFor(KeybindSettings.Actions[i]);

        Entity noteType = BuildDropdown(
            ui,
            centerX,
            top + 115,
            width,
            EditorDropdownKind.NoteType,
            labels,
            selected: (int)KeybindAction.M);

        Playhead playhead = editor.GetComponent<Playhead>();
        int snapIndex = playhead.SnapDivision switch
        {
            8 => 1,
            16 => 2,
            _ => 0,
        };
        SetDropdownSelected(snap, snapIndex);
        SetDropdownSelected(noteType, (int)KeybindAction.M);

        Entity tools = BuildPlacementTools(ui, centerX, top + 218);
        Entity saveAs = AddEditorButton(
            ui,
            new Vector2(centerX, top + 311),
            new Vector2(width, 34),
            LocalizedText.Get(EditorStrings.SaveAs),
            EditorButtonKind.SaveAs);
        Entity load = AddEditorButton(
            ui,
            new Vector2(centerX, top + 351),
            new Vector2(width, 34),
            LocalizedText.Get(EditorStrings.LoadLevel),
            EditorButtonKind.LoadLevel);

        editor.AddComponent(new EditorSidebarLayout(
            sidebarPanel,
            snapLabel,
            snap,
            typeLabel,
            noteType,
            tools,
            saveAs,
            load,
            top));
    }

    private static Entity BuildPlacementTools(
        UiBuilder ui,
        float centerX,
        float centerY)
    {
        Entity group = ui.RectEntity(
            new Vector2(centerX, centerY),
            new Vector2(190, 140),
            new Color(0, 0, 0, 0));
        group.AddComponent(new UiContainer(StackDirection.Vertical));

        AddPlacementButton(
            ui,
            group,
            new Vector2(-69, -70),
            LocalizedText.Get(EditorStrings.Note),
            new EditorPlacementButton(EditorPlacementMode.Notes),
            44);
        AddPlacementButton(
            ui,
            group,
            new Vector2(-23, -70),
            LocalizedText.Get(EditorStrings.Mine),
            new EditorPlacementButton(EditorPlacementMode.Mines),
            44);
        AddPlacementButton(
            ui,
            group,
            new Vector2(23, -70),
            LocalizedText.Get(EditorStrings.Move),
            new EditorPlacementButton(EditorPlacementMode.Movement),
            44);
        AddPlacementButton(
            ui,
            group,
            new Vector2(69, -70),
            LocalizedText.Get(EditorStrings.Start),
            new EditorPlacementButton(EditorPlacementMode.Start),
            44);

        Entity label = HudText(
            ui,
            new Vector2(0, -35),
            new Vector2(190, 18),
            LocalizedText.Get(EditorStrings.DashDir),
            12,
            SceneUi.DimTextColor);
        group.AddChild(label);

        (DashDirection Direction, string Label, Vector2 Offset)[] wheel =
        {
            (DashDirection.Up, "↑", new Vector2(0, -45)),
            (DashDirection.UpRight, "↗", new Vector2(32, -32)),
            (DashDirection.Right, "→", new Vector2(45, 0)),
            (DashDirection.DownRight, "↘", new Vector2(32, 32)),
            (DashDirection.Down, "↓", new Vector2(0, 45)),
            (DashDirection.DownLeft, "↙", new Vector2(-32, 32)),
            (DashDirection.Left, "←", new Vector2(-45, 0)),
            (DashDirection.UpLeft, "↖", new Vector2(-32, -32)),
        };

        foreach ((DashDirection direction, string text, Vector2 offset) in wheel)
        {
            AddPlacementButton(
                ui,
                group,
                new Vector2(0, 20) + offset,
                text,
                new EditorDashDirectionButton(direction),
                32,
                24);
        }

        return group;
    }

    private static void AddPlacementButton<T>(
        UiBuilder ui,
        Entity group,
        Vector2 position,
        string label,
        T component,
        float width = 56,
        float height = 28) where T : struct, IComponent
    {
        Entity button = ui.RectEntity(
            position,
            new Vector2(width, height),
            SceneUi.MutedButtonColor,
            label,
            takesInput: true);
        button.AddComponent(new TextStyle(13, SceneUi.TextColor));
        button.AddComponent(ButtonVisual.Create(SceneUi.MutedButtonColor));
        button.AddComponent(component);
        group.AddChild(button);
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
            new Vector2(-10_000, -10_000),
            Vector2.Zero,
            new Color(0, 0, 0, 0),
            takesInput: true);

        for (int i = 0; i < labels.Length; i++)
        {
            Color optionColor = kind == EditorDropdownKind.NoteType
                ? PlayfieldRenderer.ColorFor(KeybindSettings.Actions[i])
                : SceneUi.MutedButtonColor;
            Entity option = ui.RectEntity(
                new Vector2(-10_000, -10_000),
                new Vector2(width - 6, 28),
                optionColor,
                labels[i],
                takesInput: true);
            option.AddComponent(new TextStyle(13, SceneUi.TextColor));
            option.AddComponent(ButtonVisual.Create(optionColor));
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

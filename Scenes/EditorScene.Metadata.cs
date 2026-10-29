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

namespace Xanadu.Scenes;

/// <summary>Creates the initial editor state and a playfield centered between the header and footer.</summary>
public static partial class EditorScene
{
    private const float MetadataPanelWidth = 380;
    private const float MetadataPanelHeight = 370;
    private const float StartPanelHeight = 220;

    private static void BuildEditorUi(
        EntityStore entities,
        Entity editor,
        Entity root,
        Vector2 screen)
    {
        PlayfieldView view = editor.GetComponent<PlayfieldView>();
        Playhead playhead = editor.GetComponent<Playhead>();
        LevelInfo level = editor.GetComponent<LevelInfo>();

        BuildGroupOverlay(entities, root, screen);

        // Popups sit above the chrome (960) and below the header menus (970).
        UiBuilder panelUi = SceneUi.Layer(entities, root, 962);
        Vector2 home = new(
            screen.X - 24 - 190,
            EditorLayout.HeaderHeight + 8 + MetadataPanelHeight / 2);
        panelUi.Container(
            UiLayout.Hidden,
            new Vector2(MetadataPanelWidth, MetadataPanelHeight),
            StackDirection.Vertical,
            spacing: 5,
            color: SceneUi.PanelColor,
            takesInput: true);
        Entity panel = panelUi.Current;

        AddMetadataLabel(panelUi, LocalizedText.Get(EditorStrings.LevelName));
        Entity nameField = AddMetadataField(
            panelUi,
            level.Name,
            EditorFieldKind.LevelName,
            false);

        AddMetadataLabel(panelUi, LocalizedText.Get(EditorStrings.Artist));
        Entity artistField = AddMetadataField(
            panelUi,
            level.Artist,
            EditorFieldKind.Artist,
            false);

        AddMetadataLabel(panelUi, LocalizedText.Get(EditorStrings.Difficulty));
        Entity difficultyField = AddMetadataField(
            panelUi,
            level.Difficulty,
            EditorFieldKind.Difficulty,
            false);

        AddMetadataLabel(panelUi, LocalizedText.Get(EditorStrings.Bpm));
        Entity bpmField = AddMetadataField(
            panelUi,
            playhead.Bpm.ToString(),
            EditorFieldKind.Bpm,
            true);

        AddMetadataLabel(panelUi, LocalizedText.Get(EditorStrings.BeatOffset));
        Entity offsetField = AddMetadataField(
            panelUi,
            playhead.OffsetMs.ToString(),
            EditorFieldKind.Offset,
            true);

        AddMetadataLabel(panelUi, LocalizedText.Get(EditorStrings.Background));
        AddBackgroundRow(panelUi);

        editor.AddComponent(new EditorMetadataPanel(panel, home));

        UiBuilder startUi = SceneUi.Layer(entities, root, 961);
        Vector2 startHome = new(
            screen.X - 24 - 190,
            EditorLayout.HeaderHeight + 8 + MetadataPanelHeight + 8 + StartPanelHeight / 2);
        startUi.Container(
            UiLayout.Hidden,
            new Vector2(MetadataPanelWidth, StartPanelHeight),
            StackDirection.Vertical,
            spacing: 5,
            color: SceneUi.PanelColor,
            takesInput: true);
        Entity startPanel = startUi.Current;
        HudText(
            startUi,
            Vector2.Zero,
            new Vector2(340, 20),
            LocalizedText.Get(EditorStrings.StartPoint),
            14,
            SceneUi.AccentColor);
        AddMetadataLabel(startUi, LocalizedText.Get(EditorStrings.StartTime));
        AddMetadataField(
            startUi,
            "",
            EditorFieldKind.StartTime,
            true);
        AddMetadataLabel(startUi, LocalizedText.Get(EditorStrings.StartMomentumX));
        AddMetadataField(
            startUi,
            "",
            EditorFieldKind.StartMomentumX,
            true);
        AddMetadataLabel(startUi, LocalizedText.Get(EditorStrings.StartMomentumY));
        AddMetadataField(
            startUi,
            "",
            EditorFieldKind.StartMomentumY,
            true);
        editor.AddComponent(new EditorStartPanel(startPanel, startHome));

        UiBuilder helpUi = SceneUi.Layer(entities, root, 970);
        helpUi.Container(
            UiLayout.Hidden,
            new Vector2(560, 520),
            StackDirection.Vertical,
            spacing: 4,
            color: Palette.SurfaceInset,
            takesInput: true);
        Entity helpPanel = helpUi.Current;
        string[] bindings =
        {
            "EDITOR BINDINGS   (F2)",
            "SPACE                  Play / pause",
            "HOME                   Stop",
            "LEFT / RIGHT           Nudge selection",
            "SHIFT + LEFT / RIGHT   Seek 1 second",
            "MOUSE WHEEL            Seek 500 ms",
            "SHIFT + WHEEL          Seek 2 seconds",
            "T                      Tap tempo",
            $"{EditorPlacementKeybinds.LabelFor(EditorPlacementMode.Notes),-23}Place normal notes",
            $"{EditorPlacementKeybinds.LabelFor(EditorPlacementMode.Movement),-23}Place dash notes",
            $"{EditorPlacementKeybinds.LabelFor(EditorPlacementMode.Blocks),-23}Place collidable blocks",
            $"{EditorPlacementKeybinds.LabelFor(EditorPlacementMode.Spikes),-23}Place spikes (drag to paint)",
            "DELETE / BACKSPACE     Delete selection",
            "CTRL+Z / CTRL+Y        Undo / redo",
            "L                      Load song",
            "CTRL+S                 Save",
            "ESC                    Back",
            "ENTER                  Forward",
            "F5                     Play from selected start",
            "F8                     Tune shader uniforms",
            "F9                     Level shaders",
            "F10                    Toggle hitboxes",
            "F2                     Toggle this list",
        };
        foreach (string binding in bindings)
        {
            HudText(
                helpUi,
                Vector2.Zero,
                new Vector2(520, 24),
                binding,
                14,
                binding.StartsWith("EDITOR")
                    ? SceneUi.AccentColor
                    : SceneUi.TextColor);
        }

        editor.AddComponent(new EditorBindingsHelp(
            helpPanel,
            new Vector2(screen.X / 2, screen.Y / 2)));

        // Keep the fields alive through the editor component so systems can
        // discover them without relying on fragile child ordering.
        _ = (nameField, artistField, difficultyField, bpmField, offsetField);
    }

    private static void AddHudFrame(
        UiBuilder ui,
        Vector2 center,
        Vector2 size)
    {
        Color color = Palette.BorderStrong;
        Vector2 topLeft = center - size / 2;
        ui.RectEntity(
            new Vector2(center.X, topLeft.Y),
            new Vector2(size.X, 1),
            color);
        ui.RectEntity(
            new Vector2(center.X, topLeft.Y + size.Y),
            new Vector2(size.X, 1),
            color);
        ui.RectEntity(
            new Vector2(topLeft.X, center.Y),
            new Vector2(1, size.Y),
            color);
        ui.RectEntity(
            new Vector2(topLeft.X + size.X, center.Y),
            new Vector2(1, size.Y),
            color);
    }

    private static Entity HudText(
        UiBuilder ui,
        Vector2 center,
        Vector2 size,
        string text,
        int fontSize,
        Color color)
    {
        Entity entity = SceneUi.Text(
            ui,
            center,
            size,
            text,
            fontSize,
            color,
            new Color(0, 0, 0, 0));
        entity.AddComponent(new TextAlignment(TextAlign.Left));
        return entity;
    }

    private static void AddMetadataLabel(
        UiBuilder ui,
        string text)
    {
        HudText(
            ui,
            Vector2.Zero,
            new Vector2(340, 16),
            text,
            11,
            SceneUi.DimTextColor);
    }

    private static Entity AddMetadataField(
        UiBuilder ui,
        string value,
        EditorFieldKind kind,
        bool numeric)
    {
        Entity field = ui.RectEntity(
            Vector2.Zero,
            new Vector2(340, 28),
            Palette.SurfaceInset,
            value,
            takesInput: true);
        field.AddComponent(new TextStyle(14, SceneUi.TextColor));
        field.AddComponent(new TextAlignment(TextAlign.Left));
        field.AddComponent(new TextField(value, numeric));
        field.AddComponent(new EditorField(kind));
        return field;
    }

    /// <summary>
    /// The map's background colour: a live swatch, its stored hex value, and
    /// the buttons that open the picker or return the map to the theme default.
    /// </summary>
    private static void AddBackgroundRow(UiBuilder ui)
    {
        ui.Container(
            Vector2.Zero,
            new Vector2(340, 28),
            StackDirection.Horizontal,
            spacing: 6,
            color: Clear);
        Entity swatch = ui.RectEntity(
            Vector2.Zero,
            new Vector2(44, 28),
            PlayfieldRenderer.PlayfieldBackground,
            takesInput: true);
        swatch.AddComponent(new EditorBackgroundSwatch());
        swatch.AddComponent(new EditorButton(EditorButtonKind.BackgroundColor));
        swatch.AddComponent(new UiTooltip("Choose the map's background colour"));

        Entity hex = HudText(
            ui,
            Vector2.Zero,
            new Vector2(120, 28),
            "DEFAULT",
            13,
            SceneUi.DimTextColor);
        hex.AddComponent(new EditorBackgroundHex());

        Entity edit = ui.Button(
            "EDIT",
            new Vector2(80, 28),
            SceneUi.MutedButtonColor,
            fontSize: 12);
        edit.AddComponent(new EditorButton(EditorButtonKind.BackgroundColor));

        Entity reset = ui.Button(
            "RESET",
            new Vector2(76, 28),
            SceneUi.MutedButtonColor,
            fontSize: 12);
        reset.AddComponent(new EditorButton(EditorButtonKind.ResetBackgroundColor));
        reset.AddComponent(new UiTooltip("Use the theme's stage colour"));
        ui.End();
    }
}

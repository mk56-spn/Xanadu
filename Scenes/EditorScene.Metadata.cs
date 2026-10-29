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
        Playhead playhead = editor.GetComponent<Playhead>();
        LevelInfo level = editor.GetComponent<LevelInfo>();

        BuildGroupOverlay(entities, root, screen);

        // Popups sit above the chrome (960) and below the header menus (970).
        UiNode panelUi = UiNode.Layer(entities, root, 962, null, UiStage.Full);
        Vector2 home = new(
            screen.X - 24 - 190,
            EditorLayout.HeaderHeight + 8 + MetadataPanelHeight / 2);
        UiNode panelNode = panelUi.Column()
            .Size(MetadataPanelWidth, MetadataPanelHeight)
            .At(UiLayout.Hidden)
            .Gap(5)
            .Fill(SceneUi.PanelColor)
            .Input();
        Entity panel = panelNode.Entity;

        AddMetadataLabel(panelNode, LocalizedText.Get(EditorStrings.LevelName));
        Entity nameField = AddMetadataField(
            panelNode,
            level.Name,
            EditorFieldKind.LevelName,
            false);

        AddMetadataLabel(panelNode, LocalizedText.Get(EditorStrings.Artist));
        Entity artistField = AddMetadataField(
            panelNode,
            level.Artist,
            EditorFieldKind.Artist,
            false);

        AddMetadataLabel(panelNode, LocalizedText.Get(EditorStrings.Difficulty));
        Entity difficultyField = AddMetadataField(
            panelNode,
            level.Difficulty,
            EditorFieldKind.Difficulty,
            false);

        AddMetadataLabel(panelNode, LocalizedText.Get(EditorStrings.Bpm));
        Entity bpmField = AddMetadataField(
            panelNode,
            playhead.Bpm.ToString(),
            EditorFieldKind.Bpm,
            true);

        AddMetadataLabel(panelNode, LocalizedText.Get(EditorStrings.BeatOffset));
        Entity offsetField = AddMetadataField(
            panelNode,
            playhead.OffsetMs.ToString(),
            EditorFieldKind.Offset,
            true);

        AddMetadataLabel(panelNode, LocalizedText.Get(EditorStrings.Background));
        AddBackgroundRow(panelNode);

        editor.AddComponent(new EditorMetadataPanel(panel, home));

        UiNode startUi = UiNode.Layer(entities, root, 961, null, UiStage.Full);
        Vector2 startHome = new(
            screen.X - 24 - 190,
            EditorLayout.HeaderHeight + 8 + MetadataPanelHeight + 8 + StartPanelHeight / 2);
        UiNode startNode = startUi.Column()
            .Size(MetadataPanelWidth, StartPanelHeight)
            .At(UiLayout.Hidden)
            .Gap(5)
            .Fill(SceneUi.PanelColor)
            .Input();
        Entity startPanel = startNode.Entity;
        HudText(
            startNode,
            Vector2.Zero,
            new Vector2(340, 20),
            LocalizedText.Get(EditorStrings.StartPoint),
            14,
            SceneUi.AccentColor);
        AddMetadataLabel(startNode, LocalizedText.Get(EditorStrings.StartTime));
        AddMetadataField(startNode, "", EditorFieldKind.StartTime, true);
        AddMetadataLabel(startNode, LocalizedText.Get(EditorStrings.StartMomentumX));
        AddMetadataField(startNode, "", EditorFieldKind.StartMomentumX, true);
        AddMetadataLabel(startNode, LocalizedText.Get(EditorStrings.StartMomentumY));
        AddMetadataField(startNode, "", EditorFieldKind.StartMomentumY, true);
        editor.AddComponent(new EditorStartPanel(startPanel, startHome));

        UiNode helpUi = UiNode.Layer(entities, root, 970, null, UiStage.Full);
        UiNode helpNode = helpUi.Column()
            .Size(560, 520)
            .At(UiLayout.Hidden)
            .Gap(4)
            .Fill(Palette.SurfaceInset)
            .Input();
        Entity helpPanel = helpNode.Entity;
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
                helpNode,
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
        UiNode parent,
        Vector2 center,
        Vector2 size,
        string text,
        int fontSize,
        Color color) =>
        parent.Text(text, fontSize, color, TextAlign.Left)
            .Size(size)
            .At(center);

    // Temporary bridge while the chrome and groups still build on UiBuilder.
    private static Entity HudText(
        UiBuilder ui,
        Vector2 center,
        Vector2 size,
        string text,
        int fontSize,
        Color color) =>
        HudText(
            UiNode.Wrap(ui.Current, ui.Visuals),
            center,
            size,
            text,
            fontSize,
            color);

    private static void AddMetadataLabel(
        UiNode parent,
        string text) =>
        HudText(
            parent,
            Vector2.Zero,
            new Vector2(340, 16),
            text,
            11,
            SceneUi.DimTextColor);

    private static Entity AddMetadataField(
        UiNode parent,
        string value,
        EditorFieldKind kind,
        bool numeric) =>
        parent.Rect(Palette.SurfaceInset, value, new Vector2(340, 28))
            .Input()
            .With(new TextStyle(14, SceneUi.TextColor))
            .Align(TextAlign.Left)
            .With(new TextField(value, numeric))
            .With(new EditorField(kind));

    /// <summary>
    /// The map's background colour: a live swatch, its stored hex value, and
    /// the buttons that open the picker or return the map to the theme default.
    /// </summary>
    private static void AddBackgroundRow(UiNode parent)
    {
        UiNode row = parent.Row().Size(340, 28).Gap(6);

        row.Rect(PlayfieldRenderer.PlayfieldBackground, size: new Vector2(44, 28))
            .Input()
            .With(new EditorBackgroundSwatch())
            .With(new EditorButton(EditorButtonKind.BackgroundColor))
            .Tooltip("Choose the map's background colour");

        Entity hex = HudText(
            row,
            Vector2.Zero,
            new Vector2(120, 28),
            "DEFAULT",
            13,
            SceneUi.DimTextColor);
        hex.AddComponent(new EditorBackgroundHex());

        row.Button("EDIT", 12, SceneUi.MutedButtonColor)
            .Size(80, 28)
            .With(new EditorButton(EditorButtonKind.BackgroundColor));

        row.Button("RESET", 12, SceneUi.MutedButtonColor)
            .Size(76, 28)
            .With(new EditorButton(EditorButtonKind.ResetBackgroundColor))
            .Tooltip("Use the theme's stage colour");
    }
}

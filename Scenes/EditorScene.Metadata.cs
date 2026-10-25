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
    private static void BuildEditorUi(
        EntityStore entities,
        Entity editor,
        Entity root,
        Vector2 screen)
    {
        PlayfieldView view = editor.GetComponent<PlayfieldView>();
        Playhead playhead = editor.GetComponent<Playhead>();
        LevelInfo level = editor.GetComponent<LevelInfo>();

        BuildLeftMenu(entities, root, editor, screen);

        UiBuilder hudUi = SceneUi.Layer(entities, root, 945);
        float infoWidth = Mathf.Max(160, screen.X - 670);

        Entity levelText = HudText(
            hudUi,
            new Vector2(144 + infoWidth / 2, 18),
            new Vector2(infoWidth, 28),
            string.IsNullOrWhiteSpace(level.Name) ? "Untitled" : level.Name,
            22,
            SceneUi.AccentColor);
        Entity songText = HudText(
            hudUi,
            new Vector2(144 + infoWidth / 2, 40),
            new Vector2(infoWidth, 18),
            "Song: No song loaded",
            14,
            new Color("#cbd5e1"));
        Entity statusText = HudText(
            hudUi,
            new Vector2(144 + infoWidth / 2, 58),
            new Vector2(infoWidth, 18),
            "Space play   Home stop   Delete note   ←/→ nudge   Ctrl+Z/Y undo   T tap tempo   Target: select a note",
            12,
            SceneUi.DimTextColor);
        hudUi.RectEntity(
            new Vector2(170, 960),
            new Vector2(300, 36),
            new Color("#0b1220"));
        AddHudFrame(hudUi, new Vector2(170, 960), new Vector2(300, 36));
        Entity timeText = HudText(
            hudUi,
            new Vector2(170, 960),
            new Vector2(292, 30),
            "00:00.000 / 00:00.000",
            20,
            new Color("#cbd5e1"));
        Entity notesText = HudText(
            hudUi,
            new Vector2(screen.X - 140, 960),
            new Vector2(240, 16),
            "0 notes",
            13,
            SceneUi.DimTextColor);
        Entity previewText = HudText(
            hudUi,
            new Vector2(screen.X / 2, 24),
            new Vector2(screen.X - 80, 30),
            "",
            22,
            Colors.White);
        ref Position previewPosition = ref previewText.GetComponent<Position>();
        previewPosition.Value = new Vector2(-10_000, -10_000);

        editor.AddComponent(new EditorHud(
            levelText,
            songText,
            statusText,
            timeText,
            notesText,
            previewText)
        {
            PreviewHome = new Vector2(screen.X / 2, 24),
        });

        UiBuilder panelUi = SceneUi.Layer(entities, root, 947);
        Vector2 home = new(
            screen.X - 24 - 190,
            EditorLayout.HeaderHeight + 8 + 155);
        panelUi.Container(
            new Vector2(-10_000, -10_000),
            new Vector2(380, 310),
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

        editor.AddComponent(new EditorMetadataPanel(panel, home));

        UiBuilder startUi = SceneUi.Layer(entities, root, 946);
        Vector2 startHome = new(
            screen.X - 24 - 190,
            EditorLayout.HeaderHeight + 8 + 310 + 8 + 110);
        startUi.Container(
            new Vector2(-10_000, -10_000),
            new Vector2(380, 220),
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
            new Vector2(-10_000, -10_000),
            new Vector2(560, 520),
            StackDirection.Vertical,
            spacing: 4,
            color: new Color("#0b1220"),
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
            "DELETE / BACKSPACE     Delete selection",
            "CTRL+Z / CTRL+Y        Undo / redo",
            "L                      Load song",
            "CTRL+S                 Save",
            "ESC                    Back",
            "ENTER                  Forward",
            "F5                     Play from selected start",
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
        Color color = new("#475569");
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
            new Color("#0b1220"),
            value,
            takesInput: true);
        field.AddComponent(new TextStyle(14, SceneUi.TextColor));
        field.AddComponent(new TextAlignment(TextAlign.Left));
        field.AddComponent(new TextField(value, numeric));
        field.AddComponent(new EditorField(kind));
        return field;
    }
}

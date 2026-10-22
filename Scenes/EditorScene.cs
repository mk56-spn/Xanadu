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
public static class EditorScene
{
    private const int FieldSize = 1000;

    public static void Build(
        EntityStore entities,
        Vector2 viewportSize,
        Entity? parent = null,
        LevelEntry? entry = null)
    {
        float top = EditorLayout.HeaderHeight + EditorLayout.Margin;
        float bottom = viewportSize.Y -
                       EditorLayout.FooterHeight -
                       EditorLayout.TransportHeight -
                       EditorLayout.Gap -
                       EditorLayout.Margin;
        float availableHeight = Mathf.Max(1, bottom - top);
        float availableWidth = Mathf.Max(1, viewportSize.X - EditorLayout.Margin * 2);
        float renderedSize = Mathf.Min(FieldSize, Mathf.Min(availableHeight, availableWidth));
        float scale = renderedSize / FieldSize;
        float originX = (viewportSize.X - renderedSize) / 2;
        float originY = top + (availableHeight - renderedSize) / 2;

        Entity editor = entities.CreateEntity(
            new LevelInfo("Untitled", ""),
            new LevelFile(""),
            new Playfield(FieldSize, FieldSize),
            new Playhead(
                timeMs: 0,
                bpm: 120,
                offsetMs: 0,
                playing: false),
            new NoteSelection(default),
            new EditorPreview(false, 0, false),
            new EditorMetronome(false),
            new EditorTimeline(32f),
            new PlayfieldMode(PlayfieldKind.Editor),
            new PlayfieldView(new Vector2(originX, originY), scale, viewportSize));

        editor.AddComponent(new EditorSelectionBox(
            Vector2.Zero,
            Vector2.Zero,
            false));
        editor.AddComponent(new WaveformPreview(
            "",
            System.Array.Empty<float>(),
            System.Array.Empty<float>(),
            0));
        editor.AddComponent(new EditorNoteTarget(KeybindAction.M));
        editor.AddComponent(new EditorPlacementModeState(EditorPlacementMode.Notes));
        editor.AddComponent(new EditorDashDirectionState(DashDirection.UpRight));
        editor.AddComponent(new EditorUiRequest());

        if (parent is { } root)
            root.AddChild(editor);

        Entity popup = EditorWarningPopup.Build(entities);
        Entity popupLayer = popup.Parent;

        if (parent is { } sceneRoot && !popupLayer.IsNull)
            sceneRoot.AddChild(popupLayer);

        if (parent is { } menuRoot)
        {
            BuildEditorUi(entities, editor, menuRoot, viewportSize);

            BuildEditorChrome(entities, menuRoot, editor, viewportSize);
            BuildSidebar(entities, menuRoot, editor, viewportSize);
            BuildToolbar(entities, menuRoot, editor, viewportSize);
            }

        if (entry is not null)
        {
                LevelDocumentMapper.Apply(entities, editor, entry.Document);
                if (editor.HasComponent<LevelFile>())
                editor.GetComponent<LevelFile>().Path = entry.Path;
        }
    }


    private static void BuildEditorUi(
        EntityStore entities,
        Entity editor,
        Entity root,
        Vector2 screen)
    {
        PlayfieldView view = editor.GetComponent<PlayfieldView>();
        Playhead playhead = editor.GetComponent<Playhead>();
        LevelInfo level = editor.GetComponent<LevelInfo>();

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
            new Vector2(560, 466),
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

    private static void BuildEditorChrome(
        EntityStore entities,
        Entity root,
        Entity editor,
        Vector2 screen)
    {
        UiBuilder ui = SceneUi.Layer(entities, root, 960);
        ui.Container(
            screen / 2,
            screen,
            StackDirection.Vertical,
            spacing: 0,
            color: new Color(0, 0, 0, 0));
        Entity regionRoot = ui.Current;

        ui.Container(
            Vector2.Zero,
            new Vector2(screen.X, EditorLayout.HeaderHeight),
            StackDirection.Horizontal,
            spacing: 0,
            padding: 16,
            color: new Color(0, 0, 0, 0));
        SceneUi.Button(
            ui,
            Vector2.Zero,
            new Vector2(104, 38),
            LocalizedText.Get(EditorStrings.Menu),
            SceneKind.Main,
            14,
            SceneUi.MutedButtonColor);

        const float levelWidth = 104;
        const float saveWidth = 88;
        const float songWidth = 128;
        const float actionsWidth = songWidth + saveWidth + levelWidth + 16;
        ui.RectEntity(
            Vector2.Zero,
            new Vector2(
                Mathf.Max(0, screen.X - 32 - 104 - actionsWidth),
                1),
            new Color(0, 0, 0, 0));
        ui.Container(
            Vector2.Zero,
            new Vector2(actionsWidth, 38),
            StackDirection.Horizontal,
            spacing: 8,
            color: new Color(0, 0, 0, 0));
        AddEditorButtonInContainer(
            ui,
            new Vector2(songWidth, 38),
            LocalizedText.Get(EditorStrings.LoadSong),
            EditorButtonKind.LoadSong);
        AddEditorButtonInContainer(
            ui,
            new Vector2(saveWidth, 38),
            LocalizedText.Get(EditorStrings.Save),
            EditorButtonKind.Save);
        AddEditorButtonInContainer(
            ui,
            new Vector2(levelWidth, 38),
            LocalizedText.Get(EditorStrings.Level),
            EditorButtonKind.ToggleMetadata);
        ui.End();
        ui.End();

        float middleHeight = screen.Y - EditorLayout.HeaderHeight;
        ui.Container(
            Vector2.Zero,
            new Vector2(screen.X, middleHeight),
            StackDirection.Horizontal,
            spacing: 0,
            color: new Color(0, 0, 0, 0));
        ui.RectEntity(
            Vector2.Zero,
            new Vector2(Mathf.Max(0, screen.X - 210), middleHeight),
            new Color(0, 0, 0, 0));
        ui.Container(
            Vector2.Zero,
            new Vector2(210, middleHeight),
            StackDirection.Vertical,
            spacing: 0,
            color: new Color(0, 0, 0, 0));
        ui.End();
        ui.End();

        ui.Container(
            Vector2.Zero,
            new Vector2(screen.X, 200),
            StackDirection.Vertical,
            spacing: 0,
            color: new Color(0, 0, 0, 0));
        Entity footer = ui.Current;
        footer.AddTag<LayoutIgnore>();
        footer.AddComponent(new UiAnchor(
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f)));
        float[] widths = { 64, 64, 108, 108 };
        string[] labels = { "▶", "■", "Preview", "Click Off" };
        EditorButtonKind[] kinds =
        {
            EditorButtonKind.Play,
            EditorButtonKind.Stop,
            EditorButtonKind.Preview,
            EditorButtonKind.Metronome,
        };
        float transportWidth = 0;
        for (int i = 0; i < widths.Length; i++)
            transportWidth += widths[i] + (i > 0 ? 8 : 0);
        ui.Container(
            Vector2.Zero,
            new Vector2(transportWidth, 42),
            StackDirection.Horizontal,
            spacing: 8,
            color: new Color(0, 0, 0, 0));
        for (int i = 0; i < labels.Length; i++)
        {
            AddEditorButtonInContainer(
                ui,
                new Vector2(widths[i], 38),
                labels[i],
                kinds[i],
                fontSize: i < 2 ? 18 : 14);
        }
        ui.RectEntity(
            Vector2.Zero,
            new Vector2(1, 158),
            new Color(0, 0, 0, 0));
        ui.End();
        ui.End();

        _ = regionRoot;
    }

    private static void BuildToolbar(
        EntityStore entities,
        Entity root,
        Entity editor,
        Vector2 screen)
    {
        UiBuilder ui = SceneUi.Layer(entities, root, 960);

        Vector2 lens = EditorLayout.ZoomLensCenter(
            editor.GetComponent<PlayfieldView>());
        const float zoomSize = 28;
        float zoomY = lens.Y;
        float zoomX = lens.X + 20 + zoomSize / 2;

        AddEditorButton(
            ui,
            new Vector2(zoomX, zoomY),
            new Vector2(zoomSize, zoomSize),
            "-",
            EditorButtonKind.ZoomOut,
            fontSize: 18);
        AddEditorButton(
            ui,
            new Vector2(zoomX + zoomSize + 6, zoomY),
            new Vector2(zoomSize, zoomSize),
            "+",
            EditorButtonKind.ZoomIn,
            fontSize: 18);
    }

    private static Entity AddEditorButtonInContainer(
        UiBuilder ui,
        Vector2 size,
        string label,
        EditorButtonKind kind,
        int fontSize = 14)
    {
        Entity button = ui.RectEntity(
            Vector2.Zero,
            size,
            SceneUi.ButtonColor,
            label,
            takesInput: true);
        button.AddComponent(new TextStyle(fontSize, SceneUi.TextColor));
        button.AddComponent(ButtonVisual.Create(SceneUi.ButtonColor));
        button.AddComponent(new EditorButton(kind));
        return button;
    }

    private static Entity AddEditorButton(
        UiBuilder ui,
        Vector2 center,
        Vector2 size,
        string label,
        EditorButtonKind kind,
        int fontSize = 14)
    {
        Entity button = ui.RectEntity(
            center,
            size,
            SceneUi.ButtonColor,
            label,
            takesInput: true);
        button.AddComponent(new TextStyle(fontSize, SceneUi.TextColor));
        button.AddComponent(ButtonVisual.Create(SceneUi.ButtonColor));
        button.AddComponent(new EditorButton(kind));
        return button;
    }

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

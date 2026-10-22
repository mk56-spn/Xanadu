using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Position = Writer.Ecs.Position;
using Writer.Levels;
using Writer.Settings;
using Writer.Editor;
using Writer.Systems.Editor;

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
        editor.AddComponent(new EditorMineMode(false));
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

            var menu = SceneUi.Layer(entities, menuRoot, 960);
            SceneUi.Button(
                menu,
                new Vector2(74, 32),
                new Vector2(100, 28),
                "MENU",
                SceneKind.Main,
                12,
                SceneUi.MutedButtonColor);
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
        Entity timeText = HudText(
            hudUi,
            new Vector2(170, 960),
            new Vector2(300, 16),
            "00:00.000 / 00:00.000",
            14,
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

        UiBuilder panelUi = SceneUi.Layer(entities, root, 946);
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

        AddMetadataLabel(panelUi, "LEVEL NAME");
        Entity nameField = AddMetadataField(
            panelUi,
            level.Name,
            EditorFieldKind.LevelName,
            false);

        AddMetadataLabel(panelUi, "ARTIST");
        Entity artistField = AddMetadataField(
            panelUi,
            level.Artist,
            EditorFieldKind.Artist,
            false);

        AddMetadataLabel(panelUi, "DIFFICULTY");
        Entity difficultyField = AddMetadataField(
            panelUi,
            level.Difficulty,
            EditorFieldKind.Difficulty,
            false);

        AddMetadataLabel(panelUi, "BPM");
        Entity bpmField = AddMetadataField(
            panelUi,
            playhead.Bpm.ToString(),
            EditorFieldKind.Bpm,
            true);

        AddMetadataLabel(panelUi, "BEAT OFFSET (MS)");
        Entity offsetField = AddMetadataField(
            panelUi,
            playhead.OffsetMs.ToString(),
            EditorFieldKind.Offset,
            true);

        editor.AddComponent(new EditorMetadataPanel(panel, home));

        // Keep the fields alive through the editor component so systems can
        // discover them without relying on fragile child ordering.
        _ = (nameField, artistField, difficultyField, bpmField, offsetField);
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

    private static void BuildToolbar(
        EntityStore entities,
        Entity root,
        Entity editor,
        Vector2 screen)
    {
        UiBuilder ui = SceneUi.Layer(entities, root, 960);

        float right = screen.X - 174;
        float headerY = (EditorLayout.HeaderHeight - 38) / 2;
        const float levelWidth = 104;
        const float saveWidth = 88;
        const float songWidth = 128;

        AddEditorButton(
            ui,
            new Vector2(right - levelWidth + levelWidth / 2, headerY + 19),
            new Vector2(levelWidth, 38),
            "Level ▾",
            EditorButtonKind.ToggleMetadata);
        AddEditorButton(
            ui,
            new Vector2(
                right - levelWidth - 8 - saveWidth + saveWidth / 2,
                headerY + 19),
            new Vector2(saveWidth, 38),
            "Save",
            EditorButtonKind.Save);
        AddEditorButton(
            ui,
            new Vector2(
                right - levelWidth - 8 - saveWidth - 8 - songWidth + songWidth / 2,
                headerY + 19),
            new Vector2(songWidth, 38),
            "Load Song (L)",
            EditorButtonKind.LoadSong);

        Rect2 transport = EditorLayout.TransportRect(
            editor.GetComponent<PlayfieldView>());
        float[] widths = { 64, 64, 108, 108 };
        string[] labels = { "▶", "■", "Preview", "Click Off" };
        EditorButtonKind[] kinds =
        {
            EditorButtonKind.Play,
            EditorButtonKind.Stop,
            EditorButtonKind.Preview,
            EditorButtonKind.Metronome,
        };

        float total = 0;
        for (int i = 0; i < widths.Length; i++)
            total += widths[i] + (i > 0 ? 8 : 0);

        float x = screen.X / 2 - total / 2;
        float y = transport.Position.Y + transport.Size.Y / 2;

        for (int i = 0; i < widths.Length; i++)
        {
            float centerX = x + widths[i] / 2;
            AddEditorButton(
                ui,
                new Vector2(centerX, y),
                new Vector2(widths[i], 38),
                labels[i],
                kinds[i],
                fontSize: i < 2 ? 18 : 14);

            x += widths[i] + 8;
        }

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
            "SNAP",
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
            "NOTE TYPE",
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

        Entity mine = AddEditorButton(
            ui,
            new Vector2(centerX, top + 165),
            new Vector2(width, 34),
            "Mine: Off",
            EditorButtonKind.AddMine);
        Entity saveAs = AddEditorButton(
            ui,
            new Vector2(centerX, top + 205),
            new Vector2(width, 34),
            "SAVE AS",
            EditorButtonKind.SaveAs);
        Entity load = AddEditorButton(
            ui,
            new Vector2(centerX, top + 245),
            new Vector2(width, 34),
            "LOAD LEVEL",
            EditorButtonKind.LoadLevel);

        editor.AddComponent(new EditorSidebarLayout(
            sidebarPanel,
            snapLabel,
            snap,
            typeLabel,
            noteType,
            mine,
            saveAs,
            load,
            top));
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
            Entity option = ui.RectEntity(
                new Vector2(-10_000, -10_000),
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

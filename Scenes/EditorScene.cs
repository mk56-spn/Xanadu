using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Writer.Settings;
using Writer.Editor;
using Writer.Systems.Editor;
using Writer.Ui;

namespace Writer.Scenes;

/// <summary>Creates the initial editor state and a playfield centered between the header and footer.</summary>
public static class EditorScene
{
    private const int FieldSize = 1000;

    public static void Build(
        EntityStore entities,
        Vector2 viewportSize,
        Entity? parent = null)
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

        editor.AddComponent(new EditorNoteTarget(KeybindAction.M));
        editor.AddComponent(new EditorUiRequest());

        if (parent is { } root)
            root.AddChild(editor);

        Entity popup = EditorWarningPopup.Build(entities);
        Entity popupLayer = popup.Parent;

        if (parent is { } sceneRoot && !popupLayer.IsNull)
            sceneRoot.AddChild(popupLayer);

        if (parent is { } menuRoot)
        {
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
        float[] widths = { 108, 108, 108, 108, 108, 108, 108 };
        string[] labels = { "Play", "Stop", "Preview", "Click Off", "Add Move", "Zoom -", "Zoom +" };
        EditorButtonKind[] kinds =
        {
            EditorButtonKind.Play,
            EditorButtonKind.Stop,
            EditorButtonKind.Preview,
            EditorButtonKind.Metronome,
            EditorButtonKind.AddMove,
            EditorButtonKind.ZoomOut,
            EditorButtonKind.ZoomIn,
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
                kinds[i]);

            x += widths[i] + 8;
        }
    }

    private static Entity AddEditorButton(
        UiBuilder ui,
        Vector2 center,
        Vector2 size,
        string label,
        EditorButtonKind kind)
    {
        Entity button = ui.RectEntity(
            center,
            size,
            SceneUi.ButtonColor,
            label,
            takesInput: true);
        button.AddComponent(new TextStyle(14, SceneUi.TextColor));
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
        panelUi.RectEntity(
            new Vector2(centerX, top + 300),
            new Vector2(width + 20, 600),
            SceneUi.PanelColor,
            takesInput: true);

        UiBuilder ui = SceneUi.Layer(entities, root, 940);

        editor.AddComponent(new EditorSidebar(new Rect2(
            centerX - width / 2 - 10,
            top - 10,
            width + 20,
            530)));

        SceneUi.Text(
            ui,
            new Vector2(centerX, top + 20),
            new Vector2(width, 24),
            "SNAP",
            14,
            SceneUi.DimTextColor,
            new Color(0, 0, 0, 0));

        Entity snap = BuildDropdown(
            ui,
            centerX,
            top + 54,
            width,
            EditorDropdownKind.Snap,
            new[] { "1/4 beat", "1/8 beat", "1/16 beat" },
            selected: 0);

        SceneUi.Text(
            ui,
            new Vector2(centerX, top + 178),
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
            top + 212,
            width,
            EditorDropdownKind.NoteType,
            labels,
            selected: (int)KeybindAction.M);

        // The dropdowns need to know their initial editor values.
        Playhead playhead = editor.GetComponent<Playhead>();
        int snapIndex = playhead.SnapDivision switch
        {
            8 => 1,
            16 => 2,
            _ => 0,
        };
        SetDropdownSelected(snap, snapIndex);
        SetDropdownSelected(noteType, (int)KeybindAction.M);

        AddEditorButton(
            ui,
            new Vector2(centerX, top + 500),
            new Vector2(width, 34),
            "SAVE AS",
            EditorButtonKind.SaveAs);
        AddEditorButton(
            ui,
            new Vector2(centerX, top + 540),
            new Vector2(width, 34),
            "LOAD LEVEL",
            EditorButtonKind.LoadLevel);
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
            new Vector2(width, listHeight),
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
            option.AddComponent(new EditorDropdownOption(button, i));
        }

        button.AddComponent(new EditorDropdown(list, kind, selected));
        return button;
    }

    private static void SetDropdownSelected(Entity button, int selected)
    {
        if (button.IsNull || !button.HasComponent<EditorDropdown>())
            return;

        ref EditorDropdown dropdown = ref button.GetComponent<EditorDropdown>();
        dropdown.Selected = selected;
        dropdown.Open = false;
    }

}

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

        BuildHeader(entities, root, ui, screen);

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

    private const float HeaderControl = EditorShellMetrics.HeaderControlHeight;
    private const string IconDir = "res://Assets/Icons/";

    /// <summary>
    /// The shared header: the name, undo and redo at the left, and the file,
    /// song and menu dropdowns at the right, where every editor puts them.
    /// </summary>
    private static void BuildHeader(
        EntityStore entities,
        Entity root,
        UiBuilder ui,
        Vector2 screen)
    {
        const float pad = (EditorShellMetrics.HeaderHeight - HeaderControl) / 2;
        const float historyButton = 50;
        const float menuWidth = EditorShellMetrics.SettingsWidth;
        const float dropdownWidth = EditorShellMetrics.DropdownWidth;
        const float gap = EditorShellMetrics.HeaderGap;
        const float rightWidth = dropdownWidth * 2 + menuWidth + gap * 2;
        const float leftWidth = EditorShellMetrics.NameWidth + historyButton * 2 + gap * 2;
        var clear = new Color(0, 0, 0, 0);

        ui.Container(
            Vector2.Zero,
            new Vector2(screen.X, EditorLayout.HeaderHeight),
            StackDirection.Horizontal,
            spacing: gap,
            padding: pad,
            color: clear);

        ui.Container(
            Vector2.Zero,
            new Vector2(EditorShellMetrics.NameWidth, HeaderControl),
            StackDirection.Vertical,
            color: SceneUi.PanelColor);
        ui.Text(
            "LEVEL EDITOR",
            new Vector2(EditorShellMetrics.NameWidth - 10, 28),
            UiTheme.HeadingFontSize,
            SceneUi.AccentColor);
        ui.End();

        var history = new Vector2(historyButton, historyButton);
        AddEditorAction(
            ui.IconButton("UN", IconDir + "undo.svg", history, SceneUi.ButtonColor),
            EditorButtonKind.Undo);
        AddEditorAction(
            ui.IconButton("RE", IconDir + "redo.svg", history, SceneUi.ButtonColor),
            EditorButtonKind.Redo);

        ui.RectEntity(
            Vector2.Zero,
            new Vector2(Mathf.Max(0, screen.X - pad * 2 - gap * 2 - leftWidth - rightWidth), 1),
            clear);

        Entity file = ui.Button("FILE ▾", new Vector2(dropdownWidth, HeaderControl), SceneUi.MutedButtonColor);
        Entity song = ui.Button("SONG ▾", new Vector2(dropdownWidth, HeaderControl), SceneUi.MutedButtonColor);
        Entity menu = ui.Button("MENU ▾", new Vector2(menuWidth, HeaderControl), SceneUi.MutedButtonColor);
        ui.End();

        // The lists float on their own layer so opening one never moves the header.
        UiBuilder lists = SceneUi.Layer(entities, root, 970);
        HeaderMenu(lists, file,
            ("SAVE", EditorButtonKind.Save),
            ("SAVE AS", EditorButtonKind.SaveAs),
            ("LOAD LEVEL", EditorButtonKind.LoadLevel));
        HeaderMenu(lists, song,
            ("LOAD SONG", EditorButtonKind.LoadSong),
            ("TIMING", SceneActionId.SongTiming),
            ("LEVEL INFO", EditorButtonKind.ToggleMetadata));
        HeaderMenu(lists, menu,
            ("EXIT", SceneActionId.Exit));
    }

    /// <summary>A header dropdown whose options run editor buttons or scene actions.</summary>
    private static void HeaderMenu(UiBuilder lists, Entity button, params (string Label, object Action)[] entries)
    {
        var labels = new string[entries.Length];
        for (int i = 0; i < entries.Length; i++)
            labels[i] = entries[i].Label;

        UiDropdownControl dropdown = lists.DropdownList(button, labels);
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i].Action is EditorButtonKind kind)
                AddEditorAction(dropdown.Options[i], kind);
            else if (entries[i].Action is SceneActionId action)
                dropdown.Options[i].AddComponent(new SceneCommand(action));
        }
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
        AddEditorAction(button, kind);
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
        AddEditorAction(button, kind);
        return button;
    }

    private static void AddEditorAction(Entity button, EditorButtonKind kind)
    {
        SceneActionId? action = kind switch
        {
            EditorButtonKind.LoadSong => SceneActionId.LoadSong,
            EditorButtonKind.Save => SceneActionId.Save,
            EditorButtonKind.SaveAs => SceneActionId.SaveAs,
            EditorButtonKind.LoadLevel => SceneActionId.LoadLevel,
            EditorButtonKind.ToggleMetadata => SceneActionId.ToggleMetadata,
            _ => null,
        };

        if (action is { } value)
            button.AddComponent(new SceneCommand(value));
        else
            button.AddComponent(new EditorButton(kind));
    }
}

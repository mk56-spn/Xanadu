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
        Entity menu = SceneUi.ButtonEntity(
            ui,
            Vector2.Zero,
            new Vector2(104, 38),
            LocalizedText.Get(EditorStrings.Menu),
            14,
            SceneUi.MutedButtonColor);
        menu.AddComponent(new SceneCommand(SceneActionId.Exit));

        const float levelWidth = 104;
        const float saveWidth = 88;
        const float songWidth = 128;
        const float timingWidth = 96;
        const float undoWidth = 64;
        const float redoWidth = 64;
        const float actionsWidth =
            songWidth + saveWidth + levelWidth + timingWidth +
            undoWidth + redoWidth + 40;
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
        Entity timing = SceneUi.ButtonEntity(
            ui,
            Vector2.Zero,
            new Vector2(timingWidth, 38),
            "TIMING",
            14,
            SceneUi.MutedButtonColor);
        timing.AddComponent(new SceneCommand(SceneActionId.SongTiming));
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

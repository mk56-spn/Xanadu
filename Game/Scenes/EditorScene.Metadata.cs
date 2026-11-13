using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Position = Xanadu.Ecs.Position;
using Xanadu.Levels;
using Xanadu.Settings;
using Xanadu.Editor;
using Xanadu.Systems.Editor;
using Xanadu.Strings;
using Xanadu.Systems.Playfields;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Scenes;

/// <summary>Creates the initial editor state and a playfield centered between the header and footer.</summary>
public static class EditorSceneMetadata
{
    private const float MetadataPanelWidth = 380;
    private const float MetadataPanelHeight = 400;

    internal static void BuildEditorUi(
        EntityStore entities,
        Entity editor,
        Entity root,
        Vector2 screen)
    {
        Playhead playhead = editor.GetComponent<Playhead>();
        LevelInfo level = editor.GetComponent<LevelInfo>();

        EditorSceneGroups.BuildGroupOverlay(entities, root, screen);
        EditorSceneGroups.BuildGroupBar(entities, root, editor, screen);

        // Popups sit above the chrome (960) and below the header menus (970).
        UiNode panelUi = UiNode.Layer(entities, root, UiLayers.EditorMetadata, null, UiStage.Full);
        Vector2 home = new(
            screen.X - 24 - 190,
            EditorLayout.HeaderHeight + 8 + MetadataPanelHeight / 2);
        UiNode panelNode = panelUi.Column()
            .Size(MetadataPanelWidth, MetadataPanelHeight)
            .Hidden()
            .Gap(5)
            .Fill(SceneUi.PanelColor)
            .Input();
        Entity panel = panelNode.Entity;

        AddMetadataLabel(panelNode, "SONG");
        UiNode songRow = panelNode.Row().Size(340, 28).Gap(6);
        EditorSceneUi.HudText(
                songRow,
                Vector2.Zero,
                new Vector2(240, 28),
                "No song loaded",
                13,
                SceneUi.TextColor)
            .AddComponent(new EditorSongLabel());
        songRow.Button("CHANGE", 12, SceneUi.MutedButtonColor)
            .Size(94, 28)
            .With(EditorButton.Of(EditorButtonKind.LoadSong))
            .Tooltip("Pick the song this level is played to");

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

        // No BPM or offset here: songs are timed globally, on the song timing screen.

        AddMetadataLabel(panelNode, LocalizedText.Get(EditorStrings.Background));
        AddColorRow(
            panelNode,
            PlayfieldRenderer.PlayfieldBackground,
            EditorButtonKind.BackgroundColor,
            EditorButtonKind.ResetBackgroundColor,
            "Choose the map's background colour",
            EditorColorTarget.Background);

        AddMetadataLabel(panelNode, LocalizedText.Get(EditorStrings.Outside));
        AddColorRow(
            panelNode,
            PlayfieldRenderer.PlayBackground,
            EditorButtonKind.OutsideColor,
            EditorButtonKind.ResetOutsideColor,
            "Choose the colour around the playfield",
            EditorColorTarget.Outside);

        AddMetadataLabel(panelNode, LocalizedText.Get(EditorStrings.Frame));
        AddColorRow(
            panelNode,
            PlayfieldRenderer.FrameColor,
            EditorButtonKind.FrameColor,
            EditorButtonKind.ResetFrameColor,
            "Choose the colour of the playfield's frame and border line",
            EditorColorTarget.Frame);

        panel.AddComponent(new EditorPanel(EditorPanelKind.Metadata, home));


        UiNode helpUi = UiNode.Layer(entities, root, UiLayers.EditorHelp, null, UiStage.Full);
        UiNode helpNode = helpUi.Column()
            .Size(560, 560)
            .Hidden()
            .Gap(6)
            .Fill(Palette.SurfaceInset)
            .Pad(12)
            .Input();
        Entity helpPanel = helpNode.Entity;
        EditorSceneUi.HudText(
            helpNode,
            Vector2.Zero,
            new Vector2(536, 24),
            "EDITOR BINDINGS   (F9)",
            14,
            SceneUi.AccentColor);

        // The list outgrew the panel as tools were added; it scrolls now.
        helpNode.Scroll(new Vector2(536, 500), 0, out UiNode bindingList)
            .Fill(Palette.SurfaceInset);
        bindingList.IgnoreLayout()
            .AutoHeight()
            .Gap(2)
            .Main(StackMainAlignment.Start)
            .Cross(StackCrossAlignment.Start);

        string[] bindings =
        {
            "SPACE                  Play / pause",
            "HOME / END             Go to start / end",
            "UP / DOWN              Previous / next note",
            "LEFT / RIGHT           Nudge selection",
            "CTRL + LEFT / RIGHT    Step one snap",
            "SHIFT + LEFT / RIGHT   Seek 1 second",
            "F                      Fit the field view",
            "MOUSE WHEEL            Seek 500 ms",
            "SHIFT + WHEEL          Seek 2 seconds",
            "CTRL + WHEEL           Zoom field, or timeline over it",
            $"{EditorPlacementKeybinds.LabelFor(EditorPlacementMode.Notes),-23}Place action notes",
            $"{EditorPlacementKeybinds.LabelFor(EditorPlacementMode.Neutral),-23}Place neutral notes",
            $"{EditorPlacementKeybinds.LabelFor(EditorPlacementMode.Dash),-23}Place dash notes",
            $"{EditorPlacementKeybinds.LabelFor(EditorPlacementMode.Momentum),-23}Place momentum notes",
            $"{EditorPlacementKeybinds.LabelFor(EditorPlacementMode.Contact),-23}Place contact barriers",
            $"{EditorPlacementKeybinds.LabelFor(EditorPlacementMode.Mines),-23}Place mines",
            $"{EditorPlacementKeybinds.LabelFor(EditorPlacementMode.FrictionBar),-23}Place friction fields",
            $"{EditorPlacementKeybinds.LabelFor(EditorPlacementMode.Blocks),-23}Place blocks (drag to paint)",
            $"{EditorPlacementKeybinds.LabelFor(EditorPlacementMode.Spikes),-23}Place spikes (drag to paint)",
            $"{EditorPlacementKeybinds.LabelFor(EditorPlacementMode.Text),-23}Place text (edit it in the sidebar)",
            $"{"GRASS TOOL",-23}Place grass (tune it in the sidebar)",
            $"{"POLYGON TOOL",-23}Click points; first point or ENTER closes, ESC drops",
            "R                      Rotate selection a quarter turn",
            "H / V                  Flip selection across / up and down",
            "O                      Make selected blocks one-way",
            "L                      Make selected blocks ramps (right, left, off)",
            "DELETE / BACKSPACE     Delete selection",
            "CTRL+Z / CTRL+Y        Undo / redo (or CTRL+SHIFT+Z)",
            "CTRL+S                 Save",
            "ESC                    Back",
            "ENTER                  Forward",
            "F5                     Play from selected start",
            "SHIFT+F5               Play from the playhead",
            "F2                     Tune shader uniforms",
            "F10                    Level shaders",
            "F6                     Toggle hitboxes",
            "F9                     Toggle this list",
        };
        foreach (string binding in bindings)
            EditorSceneUi.HudText(bindingList, Vector2.Zero, new Vector2(520, 24), binding, 14, SceneUi.TextColor);

        helpPanel.AddComponent(new EditorPanel(
            EditorPanelKind.Bindings,
            new Vector2(screen.X / 2, screen.Y / 2)));

        // Keep the fields alive through the editor component so systems can
        // discover them without relying on fragile child ordering.
        _ = (nameField, artistField, difficultyField);
    }

    private static void AddMetadataLabel(
        UiNode parent,
        string text) =>
        EditorSceneUi.HudText(
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
    /// One of the map's colours: a live swatch, its stored hex value, and the
    /// buttons that open the picker or return it to the theme default. The
    /// marks let the metadata system find the swatch and hex it keeps current.
    /// </summary>
    private static void AddColorRow(
        UiNode parent,
        Color initial,
        EditorButtonKind edit,
        EditorButtonKind reset,
        string tooltip,
        EditorColorTarget target)
    {
        UiNode row = parent.Row().Size(340, 28).Gap(6);

        // The swatch is for dragging: drop it on another colour to copy it, or
        // drop one here. EDIT beside it opens the picker.
        UiNode swatch = row.Rect(initial, size: new Vector2(44, 28)).Input()
            .With(new EditorColorRow(target, hex: false))
            .With(new UiColorDrop())
            .Tooltip(tooltip + ". Drag it onto another colour to copy it, or drop one here");
        swatch.Entity.AddTag<UiColorSwatch>();

        Entity hex = EditorSceneUi.HudText(
            row,
            Vector2.Zero,
            new Vector2(120, 28),
            "DEFAULT",
            13,
            SceneUi.DimTextColor);
        hex.AddComponent(new EditorColorRow(target, hex: true));

        row.Button("EDIT", 12, SceneUi.MutedButtonColor)
            .Size(80, 28)
            .With(EditorButton.Of(edit));

        row.Button("RESET", 12, SceneUi.MutedButtonColor)
            .Size(76, 28)
            .With(EditorButton.Of(reset))
            .Tooltip("Use the theme's stage colour");
    }
}

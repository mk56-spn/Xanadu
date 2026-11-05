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
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Scenes;

/// <summary>
/// The editor's chrome, built as one container tree on the shared shell: a
/// header, a middle row (tool strip, viewport column, sidebar) and a footer.
/// Groups that hug an edge are anchored to their parent; everything else
/// flows, so nothing here positions a control by hand.
/// </summary>
public static partial class EditorScene
{
    private const float HeaderControl = EditorShellMetrics.HeaderControlHeight;
    private const float HeaderPad = (EditorShellMetrics.HeaderHeight - HeaderControl) / 2;

    private const string IconDir = "res://Assets/Icons/";
    private static readonly Color Clear = new(0, 0, 0, 0);

    private static void BuildEditorChrome(
        EntityStore entities,
        Entity root,
        Entity editor,
        Vector2 screen)
    {
        EditorShellRects shell = EditorShell.Compute(
            screen, new EditorShellOptions(EditorFooterHeight.Level));
        UiNode ui = UiNode.Layer(entities, root, UiLayers.EditorChrome, null, UiStage.Full);

        UiNode page = ui.Column().Box(new Rect2(Vector2.Zero, screen));

        Header header = BuildHeader(page, screen);

        UiNode middle = page.Row()
            .Size(screen.X, shell.ToolStrip.Size.Y)
            .Main(StackMainAlignment.Start);
        UiNode flyouts = UiNode.Layer(entities, root, UiLayers.EditorToolFlyout, null, UiStage.Full);
        BuildToolStrip(middle, flyouts, shell);
        BuildViewportColumn(middle, editor, shell);
        BuildSidebar(middle, editor, shell);
        BuildRailLayer(entities, root, editor, shell);

        Footer footer = BuildFooter(page, screen);

        editor.AddComponent(new EditorHud(
            header.Level,
            header.Song,
            header.Status,
            footer.Time,
            footer.Notes));

        // The lists float on their own layer so opening one never moves the header.
        UiNode lists = UiNode.Layer(entities, root, UiLayers.EditorLists, null, UiStage.Full);
        HeaderMenu(lists, header.File,
            ("SAVE", EditorButtonKind.Save),
            ("SAVE AS", EditorButtonKind.SaveAs),
            ("LOAD LEVEL", EditorButtonKind.LoadLevel));
        HeaderMenu(lists, header.SongMenu,
            ("LOAD SONG", EditorButtonKind.LoadSong),
            ("TIMING", SceneActionId.SongTiming),
            ("LEVEL INFO", EditorButtonKind.ToggleMetadata));
        HeaderMenu(lists, header.Menu,
            ("EXIT", SceneActionId.Exit));

    }

    private readonly record struct Header(
        Entity Level,
        Entity Song,
        ShellStatusLabels Status,
        Entity File,
        Entity SongMenu,
        Entity Menu);

    private readonly record struct Footer(Entity Time, Entity Notes);

    /// <summary>
    /// The name, undo and redo at the left and the dropdowns at the right, where
    /// every editor puts them. Each half is anchored to its edge.
    /// </summary>
    private static Header BuildHeader(UiNode ui, Vector2 screen)
    {
        UiNode header = ui.Row().Size(screen.X, EditorShellMetrics.HeaderHeight);

        UiNode left = EdgeGroup(header, new Vector2(0, 0.5f));
        UiNode name = left.Column()
            .Size(EditorShellMetrics.NameWidth, HeaderControl)
            .Fill(SceneUi.PanelColor)
            .Main(StackMainAlignment.Center);
        name.Text(
                "LEVEL EDITOR",
                UiType.Label,
                SceneUi.AccentColor)
            .Size(EditorShellMetrics.NameWidth - 10, HeaderControl);

        AddEditorAction(
            left.IconButton("UN", IconDir + "undo.svg", HeaderControl, SceneUi.EditorButtonColor)
                .Entity,
            EditorButtonKind.Undo);
        AddEditorAction(
            left.IconButton("RE", IconDir + "redo.svg", HeaderControl, SceneUi.EditorButtonColor)
                .Entity,
            EditorButtonKind.Redo);

        // Level and song share one line, so the header stays a single control tall.
        UiNode info = left.Row()
            .Size(HeaderInfoWidth, HeaderControl)
            .Gap(EditorShellMetrics.HeaderGap)
            .Main(StackMainAlignment.Start);
        Entity level = HudText(
            info,
            Vector2.Zero,
            new Vector2(HeaderLevelWidth, HeaderControl),
            "Untitled",
            UiType.Label,
            SceneUi.AccentColor);
        Entity song = HudText(
            info,
            Vector2.Zero,
            new Vector2(HeaderInfoWidth - HeaderLevelWidth - EditorShellMetrics.HeaderGap, HeaderControl),
            "Song: No song loaded",
            UiType.Small,
            Palette.TextMuted);

        // The last action's message and the unsaved flag, shared with the animator's header.
        ShellStatusLabels status = ShellStatus.Add(left, HeaderControl, StatusMessageWidth);

        UiNode right = EdgeGroup(header, new Vector2(1, 0.5f));
        ViewportGrid.Attach(right.Button(
                ViewportGrid.Label,
                UiTheme.FontSize,
                SceneUi.MutedButtonColor)
            .Size(ViewportGrid.ToggleWidth, HeaderControl)
            .Entity);
        PlayerPathSwitch.Attach(right.Button(
                PlayerPathSwitch.Label,
                UiTheme.FontSize,
                SceneUi.MutedButtonColor)
            .Size(PlayerPathSwitch.ToggleWidth, HeaderControl)
            .Entity);
        EditorAutoAdvance.Attach(right.Button(
                EditorAutoAdvance.Label,
                UiTheme.FontSize,
                SceneUi.MutedButtonColor)
            .Size(EditorAutoAdvance.ToggleWidth, HeaderControl)
            .Entity);
        ViewportSnap.Attach(right.Button(
                ViewportSnap.Label,
                UiTheme.FontSize,
                SceneUi.MutedButtonColor)
            .Size(ViewportSnap.ToggleWidth, HeaderControl)
            .Entity,
            "Snap blocks, spikes and notes to the grid");
        Entity file = right.Button(
                "FILE ▾",
                UiTheme.FontSize,
                SceneUi.MutedButtonColor)
            .Size(EditorShellMetrics.DropdownWidth, HeaderControl)
            .Entity;
        Entity songMenu = right.Button(
                "SONG ▾",
                UiTheme.FontSize,
                SceneUi.MutedButtonColor)
            .Size(EditorShellMetrics.DropdownWidth, HeaderControl)
            .Entity;
        Entity menu = right.Button(
                "MENU ▾",
                UiTheme.FontSize,
                SceneUi.MutedButtonColor)
            .Size(EditorShellMetrics.SettingsWidth, HeaderControl)
            .Entity;

        return new Header(level, song, status, file, songMenu, menu);
    }

    private const float HeaderInfoWidth = 420;

    /// <summary>What the header can spare for the status message between its two halves.</summary>
    private const float StatusMessageWidth = 200;
    private const float HeaderLevelWidth = 160;

    /// <summary>
    /// A group that sizes to its content and hugs one edge of its parent:
    /// anchored there and skipped by the parent's flow.
    /// </summary>
    private static UiNode EdgeGroup(UiNode ui, Vector2 anchor) =>
        ui.Row()
            .Height(EditorShellMetrics.HeaderHeight)
            .Gap(EditorShellMetrics.HeaderGap)
            .Pad(HeaderPad)
            .Anchor(anchor);

    /// <summary>A header dropdown whose options run editor buttons or scene actions.</summary>
    private static void HeaderMenu(
        UiNode lists,
        Entity button,
        params (string Label, object Action)[] entries)
    {
        var labels = new string[entries.Length];
        for (int i = 0; i < entries.Length; i++)
            labels[i] = entries[i].Label;

        UiDropdownControl dropdown = lists.DropdownList(button, labels).Control;
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i].Action is EditorButtonKind kind)
                AddEditorAction(dropdown.Options[i], kind);
            else if (entries[i].Action is SceneActionId action)
                dropdown.Options[i].AddComponent(new SceneCommand(action));
        }
    }

    /// <summary>
    /// The space between the strip and the sidebar. The playfield is drawn
    /// here by systems; this column holds the panels docked to its corners.
    /// </summary>
    private static void BuildViewportColumn(
        UiNode ui,
        Entity editor,
        EditorShellRects shell)
    {
        UiNode column = ui.Column()
            .Size(shell.Viewport.Size.X, shell.ToolStrip.Size.Y);

        BuildLeftPanel(column, editor);
        BuildNoteElements(column, editor);

        column.Row()
            .Size(shell.StatusLine.Size)
            .Fill(SceneUi.PanelColor)
            .Pad(4)
            .Main(StackMainAlignment.Start)
            .Anchor(new Vector2(0, 1));

        // The status message lives in the header, as in the animator; this line
        // keeps the shell's layout and sits under the field's mask.
    }

    /// <summary>
    /// One panel: the transport centred on its row with the note count at the
    /// right, then the clock beside the room systems draw the timeline in.
    /// </summary>
    private static Footer BuildFooter(UiNode ui, Vector2 screen)
    {
        UiNode footer = ui.Column()
            .Size(screen.X, EditorFooterHeight.Level)
            .Main(StackMainAlignment.Start);

        UiNode transport = footer.Row()
            .Size(screen.X, EditorLayout.TransportHeight)
            .Gap(8)
            .Main(StackMainAlignment.Center);

        EditorButtonKind[] kinds =
        {
            EditorButtonKind.Play,
            EditorButtonKind.Stop,
            EditorButtonKind.Preview,
            EditorButtonKind.PreviewHere,
            EditorButtonKind.Metronome,
        };
        float[] widths = { 64, 64, 108, 124, 108 };
        string[] labels = { "▶", "■", "Preview", "Play from here", "Click Off" };
        string?[] icons = { "play", "stop", null, null, null };
        for (int i = 0; i < kinds.Length; i++)
        {
            var size = new Vector2(widths[i], 38);
            if (icons[i] is { } icon)
            {
                AddEditorAction(
                    transport.IconButton(
                            labels[i],
                            IconDir + icon + ".svg",
                            size,
                            SceneUi.EditorButtonColor,
                            18)
                        .Entity,
                    kinds[i]);
            }
            else
            {
                AddEditorButton(transport, size, labels[i], kinds[i]);
            }
        }

        Entity notes = transport.Row()
            .Height(EditorLayout.TransportHeight)
            .Pad(EditorLayout.Margin)
            .Anchor(new Vector2(1, 0.5f))
            .Text("0 notes", 13, SceneUi.DimTextColor, TextAlign.Right)
            .Size(240, 16)
            .Entity;

        // The clock sits left of the timeline; systems draw the density strip,
        // the timeline and the zoom lens in the rest of this row.
        float timelineRow = EditorFooterHeight.Level - EditorLayout.TransportHeight;
        UiNode timelineSpace = footer.Row()
            .Size(screen.X, timelineRow)
            .Main(StackMainAlignment.Start);
        timelineSpace.Rect(Clear, size: new Vector2(EditorLayout.Margin, timelineRow));
        UiNode clockColumn = timelineSpace.Column()
            .Size(EditorLayout.TimeWidth, timelineRow)
            .Main(StackMainAlignment.Start);
        clockColumn.Rect(Clear, size: new Vector2(
            EditorLayout.TimeWidth,
            timelineRow - EditorLayout.TimelineBottomPad -
            (EditorLayout.TimelineHeight + EditorLayout.TimeHeight) / 2));
        Entity time = HudText(
            clockColumn.Row()
                .Size(EditorLayout.TimeWidth, EditorLayout.TimeHeight)
                .Fill(Palette.SurfaceInset)
                .Border(Palette.BorderStrong, 1)
                .Main(StackMainAlignment.Center),
            Vector2.Zero,
            new Vector2(EditorLayout.TimeWidth - 16, EditorLayout.TimeHeight - 6),
            "00:00.000 / 00:00.000",
            UiType.Body,
            Palette.TextMuted);

        return new Footer(time, notes);
    }

    private static void BuildToolbar(
        EntityStore entities,
        Entity root,
        Entity editor,
        Vector2 screen)
    {
        UiNode ui = UiNode.Layer(entities, root, UiLayers.EditorChrome, null, UiStage.Full);

        Vector2 lens = EditorLayout.ZoomLensCenter(
            editor.GetComponent<PlayfieldView>());
        const float zoomSize = 28;
        float zoomY = lens.Y;
        float zoomX = lens.X + 20 + zoomSize / 2;

        AddEditorButton(
            ui,
            new Vector2(zoomSize, zoomSize),
            "-",
            EditorButtonKind.ZoomOut,
            fontSize: 18,
            center: new Vector2(zoomX, zoomY));
        AddEditorButton(
            ui,
            new Vector2(zoomSize, zoomSize),
            "+",
            EditorButtonKind.ZoomIn,
            fontSize: 18,
            center: new Vector2(zoomX + zoomSize + 6, zoomY));
    }

    /// <summary>A styled editor button, flowing in its parent or placed explicitly.</summary>
    private static Entity AddEditorButton(
        UiNode ui,
        Vector2 size,
        string label,
        EditorButtonKind kind,
        int fontSize = 14,
        Vector2? center = null)
    {
        UiNode button = ui.Button(label, fontSize, SceneUi.EditorButtonColor).Size(size);
        if (center is { } at)
            button.At(at);
        AddEditorAction(button.Entity, kind);
        return button.Entity;
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

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
        UiNode ui = UiNode.Layer(entities, root, 960, null, UiStage.Full);

        UiNode page = ui.Column().Box(new Rect2(Vector2.Zero, screen));

        Header header = BuildHeader(page, screen);

        UiNode middle = page.Row()
            .Size(screen.X, shell.ToolStrip.Size.Y)
            .Main(StackMainAlignment.Start);
        BuildToolStrip(middle, shell);
        Entity status = BuildViewportColumn(middle, editor, shell);
        BuildSidebar(middle, editor, shell);

        Footer footer = BuildFooter(page, screen);

        editor.AddComponent(new EditorHud(
            header.Level,
            header.Song,
            status,
            footer.Time,
            footer.Notes));

        // The lists float on their own layer so opening one never moves the header.
        UiNode lists = UiNode.Layer(entities, root, 970, null, UiStage.Full);
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

        BuildDirectionWheelOverlay(entities, root, editor, shell);
    }

    private readonly record struct Header(
        Entity Level,
        Entity Song,
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
                UiTheme.HeadingFontSize,
                SceneUi.AccentColor)
            .Size(EditorShellMetrics.NameWidth - 10, 28);

        AddEditorAction(
            left.IconButton("UN", IconDir + "undo.svg", HeaderControl, SceneUi.ButtonColor)
                .Entity,
            EditorButtonKind.Undo);
        AddEditorAction(
            left.IconButton("RE", IconDir + "redo.svg", HeaderControl, SceneUi.ButtonColor)
                .Entity,
            EditorButtonKind.Redo);

        UiNode info = left.Column()
            .Size(HeaderInfoWidth, HeaderControl)
            .Cross(StackCrossAlignment.Start)
            .Main(StackMainAlignment.Center);
        Entity level = HudText(
            info,
            Vector2.Zero,
            new Vector2(HeaderInfoWidth, 28),
            "Untitled",
            22,
            SceneUi.AccentColor);
        Entity song = HudText(
            info,
            Vector2.Zero,
            new Vector2(HeaderInfoWidth, 18),
            "Song: No song loaded",
            14,
            Palette.TextMuted);

        UiNode right = EdgeGroup(header, new Vector2(1, 0.5f));
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

        return new Header(level, song, file, songMenu, menu);
    }

    private const float HeaderInfoWidth = 420;

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
    private static Entity BuildViewportColumn(
        UiNode ui,
        Entity editor,
        EditorShellRects shell)
    {
        UiNode column = ui.Column()
            .Size(shell.Viewport.Size.X, shell.ToolStrip.Size.Y);

        BuildLeftPanel(column, editor);

        UiNode line = column.Row()
            .Size(shell.StatusLine.Size)
            .Fill(SceneUi.PanelColor)
            .Pad(4)
            .Main(StackMainAlignment.Start)
            .Anchor(new Vector2(0, 1));

        return HudText(
            line,
            Vector2.Zero,
            new Vector2(shell.StatusLine.Size.X - 8, shell.StatusLine.Size.Y - 8),
            "Space play   Home stop   Delete note   ←/→ nudge   Ctrl+Z/Y undo   T tap tempo   Target: select a note",
            12,
            SceneUi.DimTextColor);
    }

    /// <summary>
    /// Transport in the middle of the row, the time at its left edge and the
    /// note count at its right, then the room systems draw the density strip
    /// and the ruler in.
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
            EditorButtonKind.Metronome,
        };
        float[] widths = { 64, 64, 108, 108 };
        string[] labels = { "▶", "■", "Preview", "Click Off" };
        string?[] icons = { "play", "stop", null, null };
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
                            SceneUi.ButtonColor,
                            18)
                        .Entity,
                    kinds[i]);
            }
            else
            {
                AddEditorButton(transport, size, labels[i], kinds[i]);
            }
        }

        UiNode timeGroup = transport.Row()
            .Height(EditorLayout.TransportHeight)
            .Pad(HeaderPad)
            .Anchor(new Vector2(0, 0.5f));
        Entity time = HudText(
            timeGroup.Row()
                .Size(300, 36)
                .Fill(Palette.SurfaceInset)
                .Border(Palette.BorderStrong, 1),
            Vector2.Zero,
            new Vector2(292, 30),
            "00:00.000 / 00:00.000",
            20,
            Palette.TextMuted);

        UiNode notesGroup = transport.Row()
            .Height(EditorLayout.TransportHeight)
            .Pad(HeaderPad)
            .Anchor(new Vector2(1, 0.5f));
        Entity notes = HudText(
            notesGroup,
            Vector2.Zero,
            new Vector2(240, 16),
            "0 notes",
            13,
            SceneUi.DimTextColor);

        // Systems draw the density strip and the ruler into this room.
        footer.Rect(
            Clear,
            size: new Vector2(
                screen.X,
                EditorFooterHeight.Level - EditorLayout.TransportHeight));

        return new Footer(time, notes);
    }

    private static void BuildToolbar(
        EntityStore entities,
        Entity root,
        Entity editor,
        Vector2 screen)
    {
        UiNode ui = UiNode.Layer(entities, root, 960, null, UiStage.Full);

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
        UiNode button = ui.Button(label, fontSize, SceneUi.ButtonColor).Size(size);
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

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
        UiBuilder ui = SceneUi.Layer(entities, root, 960);
        ui.Container(screen / 2, screen, StackDirection.Vertical, color: Clear);

        Header header = BuildHeader(ui, screen);
        ui.Container(
            Vector2.Zero,
            new Vector2(screen.X, shell.ToolStrip.Size.Y),
            StackDirection.Horizontal,
            color: Clear,
            main: StackMainAlignment.Start);
        BuildToolStrip(ui, shell);
        Entity status = BuildViewportColumn(ui, editor, shell);
        BuildSidebar(ui, editor, shell);
        ui.End();
        Footer footer = BuildFooter(ui, screen);
        ui.End();

        editor.AddComponent(new EditorHud(
            header.Level,
            header.Song,
            status,
            footer.Time,
            footer.Notes));

        // The lists float on their own layer so opening one never moves the header.
        UiBuilder lists = SceneUi.Layer(entities, root, 970);
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
    private static Header BuildHeader(UiBuilder ui, Vector2 screen)
    {
        ui.Container(
            Vector2.Zero,
            new Vector2(screen.X, EditorShellMetrics.HeaderHeight),
            StackDirection.Horizontal,
            color: Clear);

        Entity level;
        Entity song;
        EdgeGroup(ui, new Vector2(0, 0.5f));
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

        var history = new Vector2(HeaderControl, HeaderControl);
        AddEditorAction(
            ui.IconButton("UN", IconDir + "undo.svg", history, SceneUi.ButtonColor),
            EditorButtonKind.Undo);
        AddEditorAction(
            ui.IconButton("RE", IconDir + "redo.svg", history, SceneUi.ButtonColor),
            EditorButtonKind.Redo);

        ui.Container(
            Vector2.Zero,
            new Vector2(HeaderInfoWidth, HeaderControl),
            StackDirection.Vertical,
            color: Clear,
            cross: StackCrossAlignment.Start);
        level = HudText(
            ui, Vector2.Zero, new Vector2(HeaderInfoWidth, 28),
            "Untitled", 22, SceneUi.AccentColor);
        song = HudText(
            ui, Vector2.Zero, new Vector2(HeaderInfoWidth, 18),
            "Song: No song loaded", 14, Palette.TextMuted);
        ui.End();
        ui.End();

        EdgeGroup(ui, new Vector2(1, 0.5f));
        Entity file = ui.Button(
            "FILE ▾", new Vector2(EditorShellMetrics.DropdownWidth, HeaderControl), SceneUi.MutedButtonColor);
        Entity songMenu = ui.Button(
            "SONG ▾", new Vector2(EditorShellMetrics.DropdownWidth, HeaderControl), SceneUi.MutedButtonColor);
        Entity menu = ui.Button(
            "MENU ▾", new Vector2(EditorShellMetrics.SettingsWidth, HeaderControl), SceneUi.MutedButtonColor);
        ui.End();

        ui.End();
        return new Header(level, song, file, songMenu, menu);
    }

    private const float HeaderInfoWidth = 420;

    /// <summary>
    /// Opens a group that sizes to its content and hugs one edge of its parent:
    /// anchored there and skipped by the parent's flow. Close it with <c>End</c>.
    /// </summary>
    private static Entity EdgeGroup(UiBuilder ui, Vector2 anchor)
    {
        ui.Container(
            Vector2.Zero,
            new Vector2(4, EditorShellMetrics.HeaderHeight),
            StackDirection.Horizontal,
            spacing: EditorShellMetrics.HeaderGap,
            padding: HeaderPad,
            color: Clear,
            autoWidth: true);
        Entity group = ui.Current;
        group.AddTag<LayoutIgnore>();
        group.AddComponent(new UiAnchor(anchor, anchor));
        return group;
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

    /// <summary>
    /// The space between the strip and the sidebar. The playfield is drawn
    /// here by systems; this column holds the panels docked to its corners.
    /// </summary>
    private static Entity BuildViewportColumn(UiBuilder ui, Entity editor, EditorShellRects shell)
    {
        ui.Container(
            Vector2.Zero,
            new Vector2(shell.Viewport.Size.X, shell.ToolStrip.Size.Y),
            StackDirection.Vertical,
            color: Clear);

        BuildLeftPanel(ui, editor);

        ui.Container(
            Vector2.Zero,
            shell.StatusLine.Size,
            StackDirection.Horizontal,
            color: SceneUi.PanelColor,
            padding: 4,
            main: StackMainAlignment.Start);
        Entity line = ui.Current;
        line.AddTag<LayoutIgnore>();
        line.AddComponent(new UiAnchor(new Vector2(0, 1), new Vector2(0, 1)));
        Entity status = HudText(
            ui,
            Vector2.Zero,
            new Vector2(shell.StatusLine.Size.X - 8, shell.StatusLine.Size.Y - 8),
            "Space play   Home stop   Delete note   ←/→ nudge   Ctrl+Z/Y undo   T tap tempo   Target: select a note",
            12,
            SceneUi.DimTextColor);
        ui.End();

        ui.End();
        return status;
    }

    /// <summary>
    /// Transport in the middle of the row, the time at its left edge and the
    /// note count at its right, then the room systems draw the density strip
    /// and the ruler in.
    /// </summary>
    private static Footer BuildFooter(UiBuilder ui, Vector2 screen)
    {
        ui.Container(
            Vector2.Zero,
            new Vector2(screen.X, EditorFooterHeight.Level),
            StackDirection.Vertical,
            color: Clear,
            main: StackMainAlignment.Start);

        ui.Container(
            Vector2.Zero,
            new Vector2(screen.X, EditorLayout.TransportHeight),
            StackDirection.Horizontal,
            spacing: 8,
            color: Clear);

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
                    ui.IconButton(labels[i], IconDir + icon + ".svg", size, SceneUi.ButtonColor, 18),
                    kinds[i]);
            }
            else
            {
                AddEditorButtonInContainer(ui, size, labels[i], kinds[i]);
            }
        }

        ui.Container(
            Vector2.Zero,
            new Vector2(4, EditorLayout.TransportHeight),
            StackDirection.Horizontal,
            padding: HeaderPad,
            color: Clear,
            autoWidth: true);
        Entity timeGroup = ui.Current;
        timeGroup.AddTag<LayoutIgnore>();
        timeGroup.AddComponent(new UiAnchor(new Vector2(0, 0.5f), new Vector2(0, 0.5f)));
        ui.Container(
            Vector2.Zero,
            new Vector2(300, 36),
            StackDirection.Horizontal,
            color: Palette.SurfaceInset);
        ui.Current.AddComponent(new UiBorder(Palette.BorderStrong, 1));
        Entity time = HudText(
            ui, Vector2.Zero, new Vector2(292, 30),
            "00:00.000 / 00:00.000", 20, Palette.TextMuted);
        ui.End();
        ui.End();

        ui.Container(
            Vector2.Zero,
            new Vector2(4, EditorLayout.TransportHeight),
            StackDirection.Horizontal,
            padding: HeaderPad,
            color: Clear,
            autoWidth: true);
        Entity notesGroup = ui.Current;
        notesGroup.AddTag<LayoutIgnore>();
        notesGroup.AddComponent(new UiAnchor(new Vector2(1, 0.5f), new Vector2(1, 0.5f)));
        Entity notes = HudText(
            ui, Vector2.Zero, new Vector2(240, 16),
            "0 notes", 13, SceneUi.DimTextColor);
        ui.End();
        ui.End();

        // Systems draw the density strip and the ruler into this room.
        ui.RectEntity(
            Vector2.Zero,
            new Vector2(screen.X, EditorFooterHeight.Level - EditorLayout.TransportHeight),
            Clear);
        ui.End();
        return new Footer(time, notes);
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

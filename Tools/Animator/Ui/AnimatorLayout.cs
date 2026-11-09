using Friflo.Engine.ECS;
using Godot;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tools.Animator;

/// <summary>Builds the animator panels from native containers.</summary>
public static partial class AnimatorLayout
{
    // Input-capable panels draw at z 1, so the overlay canvases sit above them
    // (grid under the SVG art) and the file menu layer above the overlays.
    public const int GridZ = 2;
    public const int SvgZ = 3;
    public const int PreviewZ = 4;
    public const int TimelineZ = 5;

    // The frame comes from the shared editor shell, so the header and footer
    // sit where they do in every tool; the panels run flush to the screen edge.
    private const float Margin = 0f;
    private const float TopHeight = EditorShellMetrics.HeaderHeight;
    private const float BottomHeight = EditorFooterHeight.Animator;
    private const float Gap = 0f;
    private const float RightPanelWidth = EditorShellMetrics.RightPanelWidth;
    private const float ListWidth = RightPanelWidth - UiTheme.ContainerPadding * 2;

    private const float ButtonWidth = RightPanelWidth - UiTheme.ContainerPadding * 2;
    private const float FileMenuWidth = 180f;
    private const float ConfigMenuWidth = 150f;

    // The clip length panel: six step buttons around a numeric field. One, two
    // and three arrows step 0.1 s, 1 s and 10 s.
    public static readonly Vector2 ClipLengthSize = new(388f, 74f);
    public const float ClipLengthGap = 6f;
    private const float ClipLengthPadding = 10f;
    private const float ClipLengthSpacing = 4f;
    private const float ClipLengthArrowWidth = 40f;
    private const float ClipLengthFieldWidth = 100f;
    private const float ClipLengthRowHeight = 30f;

    // The NEW CLIP dialog, centred over a dimmer that blocks the panels below.
    public static readonly Vector2 NewClipDialogSize = new(380f, 228f);
    private const float NewClipPadding = 12f;
    private const float NewClipSpacing = 8f;
    private const float NewClipRowHeight = 30f;
    private const float NewClipButtonHeight = 32f;
    private const float NewClipLabelWidth = 70f;

    // Header slots; together they must fit in the header, which is one
    // screen wide less its padding. The window opens 1920 wide.
    private const float TitleWidth = EditorShellMetrics.NameWidth;
    /// <summary>The header's room for the status message, between undo and redo and the preview button.</summary>
    private const float StatusButtonWidth = 44f;
    private const float ClipLabelWidth = 48f;
    private const float PreviewButtonWidth = 110f;
    private const float HeaderControl = EditorShellMetrics.HeaderControlHeight;
    private const float HistoryButton = HeaderControl;
    private const float TimelineButtonGap = 4f;
    private const float TimelineGroupGap = 12f;
    private const float TimelineCaptionWidth = 480f;
    private const string IconDir = "res://Assets/Icons/";

    /// <summary>Width of the clip dropdown button in the header.</summary>
    public const float ClipWidth = 160f;

    private const float StripWidth = EditorShellMetrics.ToolStripWidth;
    private const float StripPadding = (EditorShellMetrics.ToolStripWidth - EditorShellMetrics.ToolButton) / 2;
    private static readonly Color Clear = new(0, 0, 0, 0);

    /// <summary>Creates every panel and returns the entities the systems drive.</summary>
    public static AnimatorUi Build(
        EntityStore store,
        Vector2 screen,
        IVisualRidFactory? visuals = null)
    {
        var refs = new Refs();
        EditorShellRects shell = EditorShell.Compute(
            screen, new EditorShellOptions(EditorFooterHeight.Animator));
        float mainHeight = shell.ToolStrip.Size.Y;
        UiBuilder ui = UiBuilder.Begin(store, visuals).Layer(UiLayers.Base);
        ui.RectEntity(screen / 2, screen, UiTheme.Background);

        ui.Container(screen / 2, screen, StackDirection.Vertical,
            spacing: Gap, padding: Margin, color: Clear);
        BuildHeader(ui, screen, ref refs);
        ui.Container(Vector2.Zero, new Vector2(screen.X - Margin * 2, mainHeight),
            spacing: Gap, color: Clear);
        refs.Ui.MainRow = ui.Current;
        BuildToolStrip(ui, mainHeight, ref refs);
        BuildViewport(ui, shell, ref refs);
        BuildRightPanel(ui, shell, ref refs);
        ui.End();
        BuildTimeline(ui, screen, ref refs);
        BuildPreviewBar(ui, screen, ref refs);
        ui.End();

        // One floating layer holds every menu and the history panel.
        UiBuilder menu = UiBuilder.Begin(store, visuals).Layer(UiLayers.AnimatorMenu);
        refs.Ui.MenuLayer = menu.Current;
        refs.Ui.ScreenSize = screen;

        // The inspector and the view bar are the bottom of the floating layer,
        // so menus opened over the preview still draw above them.
        BuildTabBodies(menu, shell, ref refs);
        BuildViewBar(menu, shell, ref refs);
        BuildInspector(menu, screen, ref refs);
        BuildBoneCard(menu, ref refs);
        BuildFileMenu(menu, refs.FileButton);
        BuildConfigMenu(menu, refs.ConfigButton);
        BuildClipLengthPanel(menu, ref refs);
        BuildBoneMenu(menu, ref refs);
        BuildKeyMenu(menu, ref refs);
        BuildOverrideMenu(menu, ref refs);
        BuildSvgMenu(menu, ref refs);
        BuildChainInspector(menu, ref refs);
        BuildClothInspector(menu, ref refs);
        BuildEventInspector(menu, ref refs);
        BuildNewClipDialog(menu, screen, ref refs);
        BuildInfoPanel(menu, screen, ref refs);
        refs.Ui.EasePicker = EasePickerPanel.Build(
            UiNode.Wrap(refs.Ui.MenuLayer, visuals),
            new Commands.AnimatorEasePickerHost(store));
        refs.Ui.XifEditor = XifEditorPanel.Build(
            UiNode.Wrap(refs.Ui.MenuLayer, visuals),
            XifEditorCenter(screen),
            new Commands.AnimatorXifHost(store));
        refs.Ui.ParticleEditor = ParticleEditorPanel.Build(
            UiNode.Wrap(refs.Ui.MenuLayer, visuals),
            ParticleEditorCenter(screen),
            new Commands.AnimatorParticleHost(store));
        BuildTooltip(store, visuals, ref refs);
        return refs.Ui;
    }

    private static void BuildHeader(UiBuilder ui, Vector2 screen, ref Refs refs)
    {
        ui.Container(Vector2.Zero, new Vector2(screen.X - Margin * 2, TopHeight),
            color: UiTheme.Panel);
        refs.Ui.Header = ui.Current;

        // Name, then undo and redo at the left edge and the dropdowns at the
        // right: the same places as in every editor.
        EdgeGroup(ui, new Vector2(0, 0.5f), TopHeight, EditorShellMetrics.HeaderGap,
            (TopHeight - HeaderControl) / 2);
        ui.Container(Vector2.Zero, new Vector2(TitleWidth, HeaderControl),
            color: UiTheme.PanelAlt);
        ui.Text("XAF ANIMATOR", new Vector2(TitleWidth - 10, HeaderControl), UiType.Label,
            UiTheme.Accent);
        ui.End();
        var history = new Vector2(HistoryButton, HistoryButton);
        IconAction(ui, "UN", "undo", ToolActionKind.Undo, history);
        IconAction(ui, "RE", "redo", ToolActionKind.Redo, history);

        // The unsaved flag, shared with the level editor's header; action messages are toasts.
        refs.Ui.StatusLabels = ShellStatus.Add(UiNode.Wrap(ui.Current, ui.Visuals), HeaderControl);
        refs.Ui.DirtyLabel = refs.Ui.StatusLabels.Flag;
        ui.End();

        EdgeGroup(ui, new Vector2(1, 0.5f), TopHeight, EditorShellMetrics.HeaderGap,
            (TopHeight - HeaderControl) / 2);

        // The clip dropdown's button lives in this slot, beside the file menu; its
        // list floats on the menu layer and drops over the preview. The slot is
        // horizontal on purpose: a vertical container makes room for an open
        // dropdown's list, which would push the button out of the header.
        refs.Ui.PreviewButton = Action(ui, "PREVIEW", ToolActionKind.TogglePreviewMode,
            new Vector2(PreviewButtonWidth, HeaderControl), UiTheme.SmallFontSize);
        ui.Text("CLIPS", new Vector2(ClipLabelWidth, 20), UiTheme.SmallFontSize, UiTheme.Accent);
        ui.Container(Vector2.Zero, new Vector2(ClipWidth, HeaderControl), color: Clear);
        refs.Ui.ClipList = ui.Current;
        ui.End();

        // As wide as its list, so opening the menu does not widen the header slot.
        refs.FileButton = ui.Button("FILE ▾", new Vector2(FileMenuWidth, HeaderControl));
        refs.FileButton.AddComponent(new UiTooltip("Save, load, or start a new skeleton or clip"));

        refs.Ui.ConfigButton = ui.Button("CONFIG ▾", new Vector2(ConfigMenuWidth, HeaderControl));
        refs.Ui.ConfigButton.AddComponent(new UiTooltip("Clip length, delete, and the file's info"));
        refs.ConfigButton = refs.Ui.ConfigButton;
        ui.End();
        ui.End();
    }

    /// <summary>
    /// The bar floating at the top centre of the preview, as the level editor's
    /// sits over its view: grid │ snap │ align. GRID shows the grid, SNAP puts
    /// drags on it (off to start: a whole cell is coarse for posing), ALIGN lines
    /// dragged joints up with the others. Shift while dragging overrides both.
    /// </summary>
    private static void BuildViewBar(UiBuilder menu, EditorShellRects shell, ref Refs refs)
    {
        UiNode bar = UiNode.Wrap(menu.Current, menu.Visuals).Row()
            .Size(ViewBarSize)
            .Gap(6)
            .Pad(4)
            .Solid()
            .Input()
            .At(ViewBarCenter(shell));
        refs.Ui.ViewBar = bar.Entity;

        var toggle = new Vector2(ViewBarToggleWidth, ViewBarControl);
        ViewportGrid.Attach(bar.Button(ViewportGrid.Label, UiTheme.SmallFontSize).Size(toggle).Entity);
        ViewBarSeparator(bar);
        ViewportSnap.Attach(
            bar.Button(ViewportSnap.Label, UiTheme.SmallFontSize).Size(toggle).Entity,
            "Snap dragged joints, art and IK targets to the grid; hold Shift while dragging to stay free",
            on: false);
        ViewBarSeparator(bar);
        Entity align = bar.Button(AlignLabel, UiTheme.SmallFontSize).Size(toggle).Entity;
        align.AddComponent(new Toggle(AlignLabel, AlignLabel, true));
        align.AddComponent(new ModeToggle(AnimatorMode.Align));
        AnimatorTooltips.Attach(align, ToolActionKind.ToggleMode, nameof(AnimatorMode.Align));
    }

    private const string AlignLabel = "ALIGN";
    private const float ViewBarControl = 28f;
    private const float ViewBarToggleWidth = 64f;
    private const float ViewBarTop = 6f;

    /// <summary>Three toggles, two separators, the gaps between them and the padding.</summary>
    public static readonly Vector2 ViewBarSize = new(3 * ViewBarToggleWidth + 2 * 1f + 4 * 6f + 2 * 4f, 36f);

    /// <summary>Centred over the preview, just under the header.</summary>
    public static Vector2 ViewBarCenter(EditorShellRects shell) => new(
        shell.Viewport.Position.X + shell.Viewport.Size.X / 2f,
        shell.Viewport.Position.Y + ViewBarTop + ViewBarSize.Y / 2f);

    private static void ViewBarSeparator(UiNode bar) =>
        bar.Rect(Palette.BorderStrong, size: new Vector2(1, 22));

    /// <summary>
    /// A row that sizes to its children and docks to one edge of its parent, so
    /// a bar can hold a left group and a right group with free space between.
    /// </summary>
    private static void EdgeGroup(UiBuilder ui, Vector2 anchor, float height, float spacing, float padding)
    {
        ui.Container(Vector2.Zero, new Vector2(4, height), spacing: spacing,
            padding: padding, color: Clear, autoWidth: true);
        ui.Current.AddTag<LayoutIgnore>();
        ui.Current.AddComponent(new UiAnchor(anchor, anchor));
    }

    /// <summary>A square icon button for a tool action; two letters stand in until the icon exists.</summary>
    private static Entity IconAction(
        UiBuilder ui,
        string fallback,
        string icon,
        ToolActionKind kind,
        Vector2 size)
    {
        Entity button = ui.IconButton(fallback, $"{IconDir}{icon}.svg", size);
        button.AddComponent(new ToolAction(kind));
        AnimatorTooltips.Attach(button, kind);
        return button;
    }

    /// <summary>The preview over the status line along the viewport's bottom edge.</summary>
    private static void BuildViewport(UiBuilder ui, EditorShellRects shell, ref Refs refs)
    {
        Vector2 area = shell.Viewport.Size;
        Vector2 line = shell.StatusLine.Size;
        ui.Container(Vector2.Zero, new Vector2(area.X, area.Y + line.Y),
            StackDirection.Vertical, color: Clear);
        refs.Ui.ViewportColumn = ui.Current;
        ui.Container(Vector2.Zero, area, StackDirection.Vertical,
            color: Palette.SurfaceDeep, takesInput: true);
        refs.Ui.Preview = ui.Current;
        ui.End();

        ui.Container(Vector2.Zero, line, spacing: 12, color: UiTheme.Panel,
            padding: 4, main: StackMainAlignment.Start);
        refs.Ui.StatusLine = ui.Current;

        // Framing belongs to the viewport, so it sits on the viewport's own line.
        EdgeGroup(ui, new Vector2(1, 0.5f), line.Y, 4, 2);
        var frame = new Vector2(StatusButtonWidth, line.Y - 4);
        Action(ui, "FIT", ToolActionKind.FitPreview, frame, UiTheme.SmallFontSize);
        Action(ui, "1:1", ToolActionKind.ResetPreview, frame, UiTheme.SmallFontSize);
        ui.End();
        ui.End();
        ui.End();
    }

    /// <summary>
    /// The editing modes are the tools, so they run down the shell's tool
    /// strip at the left edge: two letters each, lit while the mode is on.
    /// </summary>
    private static void BuildToolStrip(UiBuilder ui, float height, ref Refs refs)
    {
        ui.Container(Vector2.Zero, new Vector2(StripWidth, height),
            StackDirection.Vertical, spacing: EditorShellMetrics.ToolGap,
            color: UiTheme.Panel, padding: StripPadding, takesInput: true,
            main: StackMainAlignment.Start);
        refs.Ui.ToolStrip = ui.Current;
        ModeButton(ui, "AK", AnimatorMode.AutoKey);
        ModeButton(ui, "ON", AnimatorMode.Onion);
        ModeButton(ui, "RS", AnimatorMode.Rest);
        ModeButton(ui, "SV", AnimatorMode.Svg);
        ModeButton(ui, "IK", AnimatorMode.Ik);
        ModeButton(ui, "PN", AnimatorMode.Pin);
        ModeButton(ui, "KT", AnimatorMode.Kites, on: true);
        ModeButton(ui, "HD", AnimatorMode.Handles, on: true);
        ModeButton(ui, "MP", AnimatorMode.MotionPaths);
        ui.End();
    }

    private static Entity TextInput(UiBuilder ui, Vector2 size)
    {
        Entity field = ui.RectEntity(
            Vector2.Zero,
            size,
            Palette.SurfaceInset,
            "",
            takesInput: true);
        field.AddComponent(new TextField(""));
        field.AddComponent(new TextStyle(12, UiTheme.Text));
        field.AddComponent(new TextAlignment(TextAlign.Left));
        return field;
    }

    private static Entity HeaderButton(
        UiBuilder ui,
        string label,
        ToolActionKind kind,
        float width,
        string argument = "")
    {
        Entity button = ui.Button(
            label,
            new Vector2(width, AnimatorTimelineModel.HeaderHeight),
            fontSize: UiTheme.SmallFontSize);
        button.AddComponent(new ToolAction(kind, argument));
        AnimatorTooltips.Attach(button, kind, argument);
        return button;
    }

    private static Entity List(UiBuilder ui, float width = 210f)
    {
        ui.Container(Vector2.Zero, new Vector2(width, 4), StackDirection.Vertical,
            spacing: 2, color: Clear, cross: StackCrossAlignment.Start,
            autoHeight: true);
        Entity list = ui.Current;
        ui.End();
        return list;
    }

    private static void Row(UiBuilder ui, params (string Label, ToolActionKind Kind)[] buttons)
    {
        ui.Container(Vector2.Zero, new Vector2(ButtonWidth, RowHeight), spacing: 6, color: Clear);
        var size = RowButton(buttons.Length);
        foreach ((string label, ToolActionKind kind) in buttons)
            Action(ui, label, kind, size);
        ui.End();
    }

    /// <summary>A <see cref="Row"/> in the small font, for labels too long for half a button.</summary>
    private static void SmallRow(UiBuilder ui, params (string Label, ToolActionKind Kind)[] buttons)
    {
        ui.Container(Vector2.Zero, new Vector2(ButtonWidth, RowHeight), spacing: 6, color: Clear);
        var size = RowButton(buttons.Length);
        foreach ((string label, ToolActionKind kind) in buttons)
            Action(ui, label, kind, size, UiTheme.SmallFontSize);
        ui.End();
    }

    /// <summary>The buttons of a row share its width; a lone button takes all of it.</summary>
    private static Vector2 RowButton(int count) =>
        new((ButtonWidth - 6 * (count - 1)) / count, RowHeight);

    /// <summary>Panel buttons are used often but need not be large: a list row's height.</summary>
    private const float RowHeight = UiTheme.TreeRowHeight;

    private static Entity Action(
        UiBuilder ui,
        string label,
        ToolActionKind kind,
        Vector2 size,
        int fontSize = UiTheme.FontSize)
    {
        Entity button = ui.Button(label, size, fontSize: fontSize);
        button.AddComponent(new ToolAction(kind));
        AnimatorTooltips.Attach(button, kind);
        return button;
    }

    private static void ModeButton(UiBuilder ui, string label, AnimatorMode mode, bool on = false)
    {
        var size = new Vector2(EditorShellMetrics.ToolButton, EditorShellMetrics.ToolButton);
        Entity button = ui.Button(label, size);
        button.AddComponent(new Toggle(label, label, on));
        button.AddComponent(new ModeToggle(mode));
        AnimatorTooltips.Attach(button, ToolActionKind.ToggleMode, mode.ToString());
    }

    private struct Refs
    {
        public AnimatorUi Ui;
        public Entity FileButton;
        public Entity ConfigButton;
        public Entity TabStrip;
        public Entity[] TabButtons;
    }
}

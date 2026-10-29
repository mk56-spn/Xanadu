using Friflo.Engine.ECS;
using Godot;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Theme;
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
    public const int MenuLayer = 6;

    /// <summary>Above every panel and menu, so a hover label is never clipped by one.</summary>
    public const int TooltipLayer = 7;

    // The frame comes from the shared editor shell, so the header and footer
    // sit where they do in every tool; the panels run flush to the screen edge.
    private const float Margin = 0f;
    private const float TopHeight = EditorShellMetrics.HeaderHeight;
    private const float BottomHeight = EditorFooterHeight.Animator;
    private const float Gap = 0f;
    private const float RightPanelWidth = EditorShellMetrics.RightPanelWidth;
    private const float ListWidth = RightPanelWidth - UiTheme.ContainerPadding * 2;

    /// <summary>Label, button row and the spacing between them and the viewport.</summary>
    private const float LeftFixedHeight = 20f + 30f + UiTheme.ContainerSpacing * 2;
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
    // screen wide less its padding. The window is 1600 wide.
    private const float TitleWidth = EditorShellMetrics.NameWidth;
    private const float StatusWidth = 200f;
    private const float DirtyWidth = 80f;
    private const float PathWidth = 260f;
    private const float ClipLabelWidth = 48f;
    private const float HeaderControl = EditorShellMetrics.HeaderControlHeight;
    private const float HistoryButton = HeaderControl;
    private const float HeaderField = 30f;
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
        UiBuilder ui = UiBuilder.Begin(store, visuals).Layer(0);
        ui.RectEntity(screen / 2, screen, UiTheme.Background);

        ui.Container(screen / 2, screen, StackDirection.Vertical,
            spacing: Gap, padding: Margin, color: Clear);
        BuildHeader(ui, screen, ref refs);
        ui.Container(Vector2.Zero, new Vector2(screen.X - Margin * 2, mainHeight),
            spacing: Gap, color: Clear);
        BuildToolStrip(ui, mainHeight);
        BuildViewport(ui, shell, ref refs);
        BuildRightPanel(ui, shell, ref refs);
        ui.End();
        BuildTimeline(ui, screen, ref refs);
        ui.End();

        // One floating layer holds every menu and the history panel.
        UiBuilder menu = UiBuilder.Begin(store, visuals).Layer(MenuLayer);
        refs.Ui.MenuLayer = menu.Current;
        refs.Ui.ScreenSize = screen;

        // The inspector is the bottom of the floating layer, so menus opened
        // over the preview still draw above it.
        BuildTabBodies(menu, shell, ref refs);
        BuildInspector(menu, screen, ref refs);
        BuildFileMenu(menu, refs.FileButton);
        BuildConfigMenu(menu, refs.ConfigButton);
        BuildClipLengthPanel(menu, ref refs);
        BuildBoneMenu(menu, ref refs);
        BuildKeyMenu(menu, ref refs);
        BuildSvgMenu(menu, ref refs);
        BuildChainInspector(menu, ref refs);
        BuildNewClipDialog(menu, screen, ref refs);
        refs.Ui.XifEditor = XifEditorPanel.Build(
            UiNode.Wrap(refs.Ui.MenuLayer, visuals),
            XifEditorCenter(screen),
            new Commands.AnimatorXifHost(store));
        BuildTooltip(store, visuals, ref refs);
        return refs.Ui;
    }

    private static void BuildHeader(UiBuilder ui, Vector2 screen, ref Refs refs)
    {
        ui.Container(Vector2.Zero, new Vector2(screen.X - Margin * 2, TopHeight),
            color: UiTheme.Panel);

        // Name, then undo and redo at the left edge and the dropdowns at the
        // right: the same places as in every editor.
        EdgeGroup(ui, new Vector2(0, 0.5f), TopHeight, EditorShellMetrics.HeaderGap,
            (TopHeight - HeaderControl) / 2);
        ui.Container(Vector2.Zero, new Vector2(TitleWidth, HeaderControl),
            StackDirection.Vertical, spacing: 4, color: UiTheme.PanelAlt,
            cross: StackCrossAlignment.Center);
        ui.Text("XAF ANIMATOR", new Vector2(TitleWidth - 10, 28), UiTheme.HeadingFontSize,
            UiTheme.Accent);
        ui.Text("skeleton / clips / rigid SVG groups", new Vector2(TitleWidth - 10, 18),
            UiTheme.SmallFontSize, UiTheme.TextDim);
        ui.End();
        var history = new Vector2(HistoryButton, HistoryButton);
        IconAction(ui, "UN", "undo", ToolActionKind.Undo, history);
        IconAction(ui, "RE", "redo", ToolActionKind.Redo, history);
        refs.Ui.PathField = TextInput(ui, new Vector2(PathWidth, HeaderField));
        ui.End();

        EdgeGroup(ui, new Vector2(1, 0.5f), TopHeight, EditorShellMetrics.HeaderGap,
            (TopHeight - HeaderControl) / 2);

        // The clip dropdown's button lives in this slot, beside the file menu; its
        // list floats on the menu layer and drops over the preview. The slot is
        // horizontal on purpose: a vertical container makes room for an open
        // dropdown's list, which would push the button out of the header.
        ui.Text("CLIPS", new Vector2(ClipLabelWidth, 20), UiTheme.SmallFontSize, UiTheme.Accent);
        ui.Container(Vector2.Zero, new Vector2(ClipWidth, HeaderControl), color: Clear);
        refs.Ui.ClipList = ui.Current;
        ui.End();

        // As wide as its list, so opening the menu does not widen the header slot.
        refs.FileButton = ui.Button("FILE ▾", new Vector2(FileMenuWidth, HeaderControl));
        refs.FileButton.AddComponent(new UiTooltip("Save, load, or start a new skeleton or clip"));

        refs.Ui.ConfigButton = ui.Button("CONFIG ▾", new Vector2(ConfigMenuWidth, HeaderControl));
        refs.Ui.ConfigButton.AddComponent(new UiTooltip("Clip length and delete"));
        refs.ConfigButton = refs.Ui.ConfigButton;
        ui.End();
        ui.End();
    }

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

    /// <summary>The bones, attachments and chains: the tree on top of the right panel.</summary>
    private static void BuildTree(UiBuilder ui, float height, ref Refs refs)
    {
        ui.Panel(Vector2.Zero, new Vector2(RightPanelWidth, height),
            cross: StackCrossAlignment.Start, main: StackMainAlignment.Start);
        ui.Text("BONES", new Vector2(ListWidth, 20), UiTheme.FontSize, UiTheme.Accent);
        Row(ui, ("+ BONE", ToolActionKind.AddBone), ("DEL", ToolActionKind.DeleteBone));

        // Bones and attachments share one scrolling viewport so long rigs
        // never spill out of the panel over the timeline.
        float viewportHeight = height - UiTheme.ContainerPadding * 2 - LeftFixedHeight;
        ui.Container(Vector2.Zero, new Vector2(ListWidth, viewportHeight),
            StackDirection.Vertical, clip: true, color: UiTheme.Panel,
            takesInput: false);
        Entity viewport = ui.Current;
        ui.Container(Vector2.Zero, new Vector2(ListWidth, 4), StackDirection.Vertical,
            spacing: UiTheme.ContainerSpacing, color: Clear,
            cross: StackCrossAlignment.Start, autoHeight: true,
            main: StackMainAlignment.Start);
        Entity content = ui.Current;
        refs.Ui.BoneList = ui.Tree(ListWidth - 10);
        ui.Text("ATTACHMENTS", new Vector2(ListWidth, 20), UiTheme.FontSize, UiTheme.Accent);
        refs.Ui.AttachmentList = List(ui, ListWidth - 10);
        ui.Text("CHAINS", new Vector2(ListWidth, 20), UiTheme.FontSize, UiTheme.Accent);
        refs.Ui.ChainList = List(ui, ListWidth - 10);
        ui.Text("EFFECTS", new Vector2(ListWidth, 20), UiTheme.FontSize, UiTheme.Accent);
        refs.Ui.EffectList = List(ui, ListWidth - 10);
        ui.End();
        ui.End();
        viewport.AddComponent(new ScrollView(content));
        ui.End();
    }

    /// <summary>The preview over the status line along the viewport's bottom edge.</summary>
    private static void BuildViewport(UiBuilder ui, EditorShellRects shell, ref Refs refs)
    {
        Vector2 area = shell.Viewport.Size;
        Vector2 line = shell.StatusLine.Size;
        ui.Container(Vector2.Zero, new Vector2(area.X, area.Y + line.Y),
            StackDirection.Vertical, color: Clear);
        ui.Container(Vector2.Zero, area, StackDirection.Vertical,
            color: Palette.SurfaceDeep, takesInput: true);
        refs.Ui.Preview = ui.Current;
        ui.End();

        ui.Container(Vector2.Zero, line, spacing: 12, color: UiTheme.Panel,
            padding: 4, main: StackMainAlignment.Start);
        refs.Ui.Status = ui.Text("", new Vector2(StatusWidth, line.Y - 8), UiTheme.FontSize,
            Palette.Lime, TextAlign.Left);
        refs.Ui.DirtyLabel = ui.Text("", new Vector2(DirtyWidth, line.Y - 8), UiTheme.SmallFontSize,
            UiTheme.Accent, TextAlign.Left);
        ui.End();
        ui.End();
    }

    /// <summary>
    /// The editing modes are the tools, so they run down the shell's tool
    /// strip at the left edge: two letters each, lit while the mode is on.
    /// </summary>
    private static void BuildToolStrip(UiBuilder ui, float height)
    {
        ui.Container(Vector2.Zero, new Vector2(StripWidth, height),
            StackDirection.Vertical, spacing: EditorShellMetrics.ToolGap,
            color: UiTheme.Panel, padding: StripPadding, takesInput: true,
            main: StackMainAlignment.Start);
        ModeButton(ui, "AK", AnimatorMode.AutoKey);
        ModeButton(ui, "ON", AnimatorMode.Onion);
        ModeButton(ui, "RS", AnimatorMode.Rest);
        ModeButton(ui, "SV", AnimatorMode.Svg);
        ModeButton(ui, "IK", AnimatorMode.Ik);
        ModeButton(ui, "PN", AnimatorMode.Pin);
        ModeButton(ui, "KT", AnimatorMode.Kites, on: true);
        ModeButton(ui, "HD", AnimatorMode.Handles, on: true);
        ui.End();
    }

    /// <summary>
    /// The tree on top, then the tab strip, then room for the selected tab's
    /// body, which floats over the room (see <see cref="BuildTabBodies"/>).
    /// </summary>
    private static void BuildRightPanel(UiBuilder ui, EditorShellRects shell, ref Refs refs)
    {
        ui.Container(Vector2.Zero, shell.RightPanel.Size, StackDirection.Vertical,
            color: UiTheme.Panel, main: StackMainAlignment.Start);
        BuildTree(ui, shell.RightTree.Size.Y, ref refs);

        ui.Container(Vector2.Zero, shell.RightTabs.Size, color: Clear);
        refs.TabStrip = ui.Current;
        refs.TabButtons = new Entity[TabNames.Length];
        for (int i = 0; i < TabNames.Length; i++)
        {
            Rect2 tab = EditorShell.Tab(shell, i, TabNames.Length);
            refs.TabButtons[i] = ui.Button(TabNames[i], tab.Size,
                fontSize: UiTheme.SmallFontSize);
        }
        ui.End();

        ui.RectEntity(Vector2.Zero, shell.RightBody.Size, Clear);
        ui.End();
    }

    private static readonly string[] TabNames = { "POSE", "EDIT", "FLOW" };

    /// <summary>
    /// The tab bodies float on the menu layer: a container in a stack cannot be
    /// hidden, so each body is parked off screen until its tab is selected.
    /// </summary>
    private static void BuildTabBodies(UiBuilder menu, EditorShellRects shell, ref Refs refs)
    {
        var bodies = new Entity[TabNames.Length];
        for (int i = 0; i < bodies.Length; i++)
        {
            menu.Container(UiLayout.Hidden, shell.RightBody.Size, StackDirection.Vertical,
                spacing: UiTheme.ContainerSpacing, color: UiTheme.Panel,
                padding: UiTheme.ContainerPadding, takesInput: true,
                cross: StackCrossAlignment.Center, main: StackMainAlignment.Start);
            bodies[i] = menu.Current;
            if (i == 0)
            {
                Row(menu, ("ROT-", ToolActionKind.RotateLeft), ("ROT+", ToolActionKind.RotateRight));
                Row(menu, ("SCL-", ToolActionKind.ScaleDown), ("SCL+", ToolActionKind.ScaleUp));
                Row(menu, ("KEY", ToolActionKind.KeyBone), ("POSE", ToolActionKind.KeyPose));
                Row(menu, ("MIRROR", ToolActionKind.MirrorPose), ("MOVE", ToolActionKind.ToggleMovable));
            }
            else if (i == 2)
            {
                BuildFlowBody(menu, shell.RightBody.Size, ref refs);
            }
            else
            {
                SmallRow(menu, ("C POSE", ToolActionKind.CopyPose), ("P POSE", ToolActionKind.PastePose));
                SmallRow(menu, ("C KEYS", ToolActionKind.CopyKeys), ("P KEYS", ToolActionKind.PasteKeys));
                Row(menu, ("FIT", ToolActionKind.FitPreview), ("1:1", ToolActionKind.ResetPreview));
                SmallRow(menu, ("+ STREAK", ToolActionKind.AddEffect), ("DEL FX", ToolActionKind.DeleteEffect));
                SmallRow(menu, ("FADE -", ToolActionKind.EffectOpacityDown), ("FADE +", ToolActionKind.EffectOpacityUp));
                SmallRow(menu, ("SHOW/HIDE", ToolActionKind.ToggleEffectVisible));
            }
            menu.End();
        }

        refs.TabStrip.AddComponent(new UiTabs(
            refs.TabButtons,
            bodies,
            shell.RightBody.Position + shell.RightBody.Size / 2));
    }

    private static void BuildTimeline(UiBuilder ui, Vector2 screen, ref Refs refs)
    {
        float width = screen.X - Margin * 2;
        ui.Container(Vector2.Zero, new Vector2(width, BottomHeight),
            StackDirection.Vertical, color: UiTheme.Panel, padding: 10,
            takesInput: true, main: StackMainAlignment.Start);
        refs.Ui.Timeline = ui.Current;

        // Transport and the caption at the left; the clip's controls at the right,
        // in groups: interpolation, name, preview, loop.
        float h = AnimatorTimelineModel.HeaderHeight;
        ui.Container(Vector2.Zero, new Vector2(width - 20, h), color: Clear);
        EdgeGroup(ui, new Vector2(0, 0.5f), h, TimelineGroupGap, 0);
        ui.Container(Vector2.Zero, new Vector2(4, h), spacing: TimelineButtonGap,
            color: Clear, autoWidth: true);
        var transport = new Vector2(AnimatorTimelineModel.TransportButtonWidth, h);
        IconAction(ui, "PL", "play", ToolActionKind.Play, transport);
        IconAction(ui, "ST", "stop", ToolActionKind.Stop, transport);
        ui.End();
        refs.Ui.TimelineCaption = ui.Text("", new Vector2(TimelineCaptionWidth, h),
            UiTheme.SmallFontSize, UiTheme.TextDim, TextAlign.Left);
        ui.End();

        EdgeGroup(ui, new Vector2(1, 0.5f), h, TimelineGroupGap, 0);
        ui.Container(Vector2.Zero, new Vector2(4, h), spacing: TimelineButtonGap,
            color: Clear, autoWidth: true);
        InterpolationButton(ui, "STEP", XafEasePreset.Step, 44);
        InterpolationButton(ui, "LINEAR", XafEasePreset.Linear, 56);
        InterpolationButton(ui, "EASE", XafEasePreset.Ease, 48);
        InterpolationButton(ui, "IN", XafEasePreset.EaseIn, 34);
        InterpolationButton(ui, "OUT", XafEasePreset.EaseOut, 40);
        ui.End();
        refs.Ui.ClipName = ClipNameField(ui);

        // Game preview and loop settings stay above the timeline, side by side,
        // because they change what the clip does rather than the clip itself.
        ui.Container(Vector2.Zero, new Vector2(4, h), spacing: TimelineButtonGap,
            color: Clear, autoWidth: true);
        HeaderButton(ui, "GAME", ToolActionKind.SequenceGame, 52);
        HeaderButton(ui, "ALL", ToolActionKind.SequenceAll, 40);
        ui.End();
        refs.Ui.LoopButton = HeaderButton(ui, "LOOP", ToolActionKind.ToggleLoop, 76);
        ui.End();
        ui.End();
        ui.End();
    }

    private static Entity ClipNameField(UiBuilder ui) =>
        TextInput(ui, new Vector2(150, AnimatorTimelineModel.HeaderHeight));

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

    private static void InterpolationButton(UiBuilder ui, string label, XafEasePreset preset, float width)
    {
        HeaderButton(ui, label, ToolActionKind.Interpolate, width, preset.ToString());
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
        ui.Container(Vector2.Zero, new Vector2(ButtonWidth, 30), spacing: 6, color: Clear);
        var size = new Vector2((ButtonWidth - 6) / 2, 30);
        foreach ((string label, ToolActionKind kind) in buttons)
            Action(ui, label, kind, size);
        ui.End();
    }

    /// <summary>A <see cref="Row"/> in the small font, for labels too long for half a button.</summary>
    private static void SmallRow(UiBuilder ui, params (string Label, ToolActionKind Kind)[] buttons)
    {
        ui.Container(Vector2.Zero, new Vector2(ButtonWidth, 30), spacing: 6, color: Clear);
        var size = new Vector2((ButtonWidth - 6) / 2, 30);
        foreach ((string label, ToolActionKind kind) in buttons)
            Action(ui, label, kind, size, UiTheme.SmallFontSize);
        ui.End();
    }

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

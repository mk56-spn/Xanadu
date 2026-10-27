using Friflo.Engine.ECS;
using Godot;
using Writer.Animation;
using Writer.Ecs;
using Position = Writer.Ecs.Position;

namespace Writer.Tools.Animator;

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

    private const float Margin = 16f;
    private const float TopHeight = 88f;
    private const float BottomHeight = 216f;
    private const float Gap = 12f;
    private const float LeftWidth = 240f;
    private const float RightWidth = 300f;
    private const float ListWidth = LeftWidth - UiTheme.ContainerPadding * 2;

    /// <summary>Label, button row and the spacing between them and the viewport.</summary>
    private const float LeftFixedHeight = 20f + 30f + UiTheme.ContainerSpacing * 2;
    private const float ButtonWidth = 168f;
    private const float FileMenuWidth = 180f;
    private const float ClipMenuWidth = 120f;

    // Header slots; together they must fit in the header, which is one
    // screen wide less its margins and padding.
    private const float TitleWidth = 250f;
    private const float StatusWidth = 220f;
    private const float DirtyWidth = 100f;
    private const float PathWidth = 290f;
    private const float ClipLabelWidth = 48f;

    /// <summary>Width of the clip dropdown button in the header.</summary>
    public const float ClipWidth = 160f;

    // The floating undo/redo panel sits at the preview's top-left corner,
    // just right of the bones list and under the preview's clip/time caption.
    private const float HistoryInset = 12f;
    private const float HistoryTop = 40f;

    /// <summary>Height of the horizontal mode bar above the preview, and the gap under it.</summary>
    private const float ModeBarHeight = 36f;
    private const float ModeBarGap = 8f;
    private const float ModeInset = ModeBarHeight + ModeBarGap;
    private const float HistoryPadding = 8f;
    private const float HistorySpacing = 6f;
    private const float HistoryButtonWidth = 76f;
    private const float HistoryButtonHeight = 30f;
    private static readonly Color Clear = new(0, 0, 0, 0);

    /// <summary>Creates every panel and returns the entities the systems drive.</summary>
    public static AnimatorUi Build(
        EntityStore store,
        Vector2 screen,
        IVisualRidFactory? visuals = null)
    {
        var refs = new Refs();
        float mainHeight = screen.Y - Margin * 2 - TopHeight - BottomHeight - Gap * 2;
        UiBuilder ui = UiBuilder.Begin(store, visuals).Layer(0);
        ui.RectEntity(screen / 2, screen, UiTheme.Background);

        ui.Container(screen / 2, screen, StackDirection.Vertical,
            spacing: Gap, padding: Margin, color: Clear);
        BuildHeader(ui, screen, ref refs);
        ui.Container(Vector2.Zero, new Vector2(screen.X - Margin * 2, mainHeight),
            spacing: Gap, color: Clear);
        BuildLeft(ui, mainHeight, ref refs);
        BuildPreview(ui, screen, mainHeight, ref refs);
        BuildRight(ui, mainHeight, ref refs);
        ui.End();
        BuildTimeline(ui, screen, ref refs);
        ui.End();

        // One floating layer holds every menu and the history panel.
        UiBuilder menu = UiBuilder.Begin(store, visuals).Layer(MenuLayer);
        refs.Ui.MenuLayer = menu.Current;
        refs.Ui.ScreenSize = screen;

        // The inspector is the bottom of the floating layer, so menus opened
        // over the preview still draw above it.
        BuildInspector(menu, screen, ref refs);
        BuildFileMenu(menu, refs.FileButton);
        BuildClipMenu(menu, refs.ClipMenuButton);
        BuildBoneMenu(menu, ref refs);
        BuildKeyMenu(menu, ref refs);
        BuildSvgMenu(menu, ref refs);
        BuildHistoryPanel(menu);
        BuildTooltip(store, visuals, ref refs);
        return refs.Ui;
    }

    private static void BuildHeader(UiBuilder ui, Vector2 screen, ref Refs refs)
    {
        ui.Container(Vector2.Zero, new Vector2(screen.X - Margin * 2, TopHeight),
            spacing: 16, color: UiTheme.Panel, padding: 14);
        ui.Container(Vector2.Zero, new Vector2(TitleWidth, TopHeight - 28),
            StackDirection.Vertical, spacing: 4, color: Clear,
            cross: StackCrossAlignment.Start);
        ui.Text("XAF ANIMATOR", new Vector2(TitleWidth - 10, 28), UiTheme.HeadingFontSize,
            UiTheme.Accent);
        ui.Text("skeleton / clips / rigid SVG groups", new Vector2(TitleWidth - 10, 18),
            UiTheme.SmallFontSize, UiTheme.TextDim);
        ui.End();

        refs.Ui.Status = ui.Text("", new Vector2(StatusWidth, 24), UiTheme.FontSize,
            new Color("#a3e635"));
        refs.Ui.DirtyLabel = ui.Text("", new Vector2(DirtyWidth, 24), UiTheme.SmallFontSize,
            UiTheme.Accent);
        refs.Ui.PathField = TextInput(ui, new Vector2(PathWidth, 26));

        // The clip dropdown's button lives in this slot, beside the file menu; its
        // list floats on the menu layer and drops over the preview, clear of the
        // history panel. The slot is horizontal on purpose: a vertical container
        // makes room for an open dropdown's list, which would push the button
        // out of the header.
        ui.Text("CLIPS", new Vector2(ClipLabelWidth, 20), UiTheme.SmallFontSize, UiTheme.Accent);
        ui.Container(Vector2.Zero, new Vector2(ClipWidth, 30), color: Clear);
        refs.Ui.ClipList = ui.Current;
        ui.End();

        // As wide as its list, so opening the menu does not widen the header slot.
        refs.FileButton = ui.Button("FILE ▾", new Vector2(FileMenuWidth, 30));
        refs.FileButton.AddComponent(new UiTooltip("Save, load or start a new skeleton"));

        // The clip's length, duplicate and delete are rarely touched, so they live in a menu.
        refs.ClipMenuButton = ui.Button("CLIP ▾", new Vector2(ClipMenuWidth, 30));
        refs.ClipMenuButton.AddComponent(new UiTooltip("Change the clip's length, duplicate or delete it"));
        ui.End();
    }

    private static void BuildLeft(UiBuilder ui, float height, ref Refs refs)
    {
        ui.Panel(Vector2.Zero, new Vector2(LeftWidth, height),
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
        ui.End();
        ui.End();
        viewport.AddComponent(new ScrollView(content));
        ui.End();
    }

    private static void BuildPreview(UiBuilder ui, Vector2 screen, float height, ref Refs refs)
    {
        float width = screen.X - Margin * 2 - LeftWidth - RightWidth - Gap * 2;
        ui.Container(Vector2.Zero, new Vector2(width, height),
            StackDirection.Vertical, spacing: ModeBarGap, color: Clear);
        BuildModeBar(ui, width);
        ui.Container(Vector2.Zero, new Vector2(width, height - ModeInset),
            StackDirection.Vertical, color: new Color("#0f172a"), takesInput: true);
        refs.Ui.Preview = ui.Current;
        ui.End();
        ui.End();
    }

    /// <summary>
    /// Editing modes run horizontally over the viewport: they change what the
    /// preview does, so they belong above it rather than in the action column.
    /// </summary>
    private static void BuildModeBar(UiBuilder ui, float width)
    {
        ui.Container(Vector2.Zero, new Vector2(width, ModeBarHeight),
            spacing: 6, color: UiTheme.Panel, padding: 4, takesInput: true,
            main: StackMainAlignment.Start);
        ui.Text("MODES", new Vector2(48, 20), 11, UiTheme.TextDim, TextAlign.Left);
        ModeButton(ui, "AUTO", AnimatorMode.AutoKey, 78);
        ModeButton(ui, "ONION", AnimatorMode.Onion, 78);
        ModeButton(ui, "REST", AnimatorMode.Rest, 72);
        ModeButton(ui, "SVG", AnimatorMode.Svg, 66);
        ModeButton(ui, "IK", AnimatorMode.Ik, 60);
        ModeButton(ui, "PIN", AnimatorMode.Pin, 66);
        ui.End();
    }

    private static void BuildRight(UiBuilder ui, float height, ref Refs refs)
    {
        ui.Panel(Vector2.Zero, new Vector2(RightWidth, height),
            cross: StackCrossAlignment.Center, main: StackMainAlignment.Start);
        Row(ui, ("PLAY", ToolActionKind.Play), ("STOP", ToolActionKind.Stop));
        Row(ui, ("ROT-", ToolActionKind.RotateLeft), ("ROT+", ToolActionKind.RotateRight));
        Row(ui, ("SCL-", ToolActionKind.ScaleDown), ("SCL+", ToolActionKind.ScaleUp));
        Row(ui, ("KEY", ToolActionKind.KeyBone), ("POSE", ToolActionKind.KeyPose));
        SmallRow(ui, ("C POSE", ToolActionKind.CopyPose), ("P POSE", ToolActionKind.PastePose));
        Row(ui, ("MIRROR", ToolActionKind.MirrorPose), ("MOVE", ToolActionKind.ToggleMovable));
        SmallRow(ui, ("C KEYS", ToolActionKind.CopyKeys), ("P KEYS", ToolActionKind.PasteKeys));
        Row(ui, ("FIT", ToolActionKind.FitPreview), ("1:1", ToolActionKind.ResetPreview));
        ui.End();
    }

    private static void BuildTimeline(UiBuilder ui, Vector2 screen, ref Refs refs)
    {
        float width = screen.X - Margin * 2;
        ui.Container(Vector2.Zero, new Vector2(width, BottomHeight),
            StackDirection.Vertical, color: UiTheme.Panel, padding: 10,
            takesInput: true, main: StackMainAlignment.Start);
        refs.Ui.Timeline = ui.Current;

        // The clip controls sit right-aligned in the header, over the drawn caption.
        ui.Container(Vector2.Zero, new Vector2(width - 20, AnimatorTimelineModel.HeaderHeight),
            spacing: 6, color: Clear, main: StackMainAlignment.End);
        InterpolationButton(ui, "STEP", XafEasePreset.Step, 44);
        InterpolationButton(ui, "LINEAR", XafEasePreset.Linear, 56);
        InterpolationButton(ui, "EASE", XafEasePreset.Ease, 48);
        InterpolationButton(ui, "IN", XafEasePreset.EaseIn, 34);
        InterpolationButton(ui, "OUT", XafEasePreset.EaseOut, 40);
        ui.RectEntity(Vector2.Zero, new Vector2(14, AnimatorTimelineModel.HeaderHeight), Clear);
        refs.Ui.ClipName = ClipNameField(ui);
        HeaderButton(ui, "GAME", ToolActionKind.SequenceGame, 52);
        HeaderButton(ui, "ALL", ToolActionKind.SequenceAll, 40);
        HeaderButton(ui, "NEW", ToolActionKind.NewClip, 44);
        refs.Ui.LoopButton = HeaderButton(ui, "LOOP", ToolActionKind.ToggleLoop, 76);
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
            new Color("#0b1220"),
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

    /// <summary>
    /// Undo and redo in their own small panel floating over the preview's
    /// top-left corner, right of the bones list. It takes input so clicks on
    /// its padding do not fall through and start a drag in the preview.
    /// </summary>
    private static void BuildHistoryPanel(UiBuilder menu)
    {
        var size = new Vector2(
            HistoryButtonWidth * 2 + HistorySpacing + HistoryPadding * 2,
            HistoryButtonHeight + HistoryPadding * 2);
        var topLeft = new Vector2(
            Margin + LeftWidth + Gap + HistoryInset,
            Margin + TopHeight + Gap + ModeInset + HistoryTop);
        menu.Container(topLeft + size / 2, size, StackDirection.Horizontal,
            spacing: HistorySpacing, color: UiTheme.Panel, padding: HistoryPadding,
            takesInput: true);
        var button = new Vector2(HistoryButtonWidth, HistoryButtonHeight);
        Action(menu, "UNDO", ToolActionKind.Undo, button);
        Action(menu, "REDO", ToolActionKind.Redo, button);
        menu.End();
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

    private static void ModeButton(UiBuilder ui, string label, AnimatorMode mode, float width)
    {
        Entity button = ui.Button(
            $"{label} OFF",
            new Vector2(width, 28),
            fontSize: UiTheme.SmallFontSize);
        button.AddComponent(new Toggle($"{label} ON", $"{label} OFF", false));
        button.AddComponent(new ModeToggle(mode));
        AnimatorTooltips.Attach(button, ToolActionKind.ToggleMode, mode.ToString());
    }

    private struct Refs
    {
        public AnimatorUi Ui;
        public Entity FileButton;
        public Entity ClipMenuButton;
    }
}

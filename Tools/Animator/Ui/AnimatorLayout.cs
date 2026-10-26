using Friflo.Engine.ECS;
using Godot;
using Writer.Animation;
using Writer.Ecs;
using Position = Writer.Ecs.Position;

namespace Writer.Tools.Animator;

/// <summary>Builds the animator panels from native containers.</summary>
public static class AnimatorLayout
{
    // Input-capable panels draw at z 1, so the overlay canvases sit above them
    // (grid under the SVG art) and the file menu layer above the overlays.
    public const int GridZ = 2;
    public const int SvgZ = 3;
    public const int PreviewZ = 4;
    public const int TimelineZ = 5;
    public const int MenuLayer = 6;

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
    private const float PathWidth = 400f;
    private static readonly Vector2 ButtonSize = new(ButtonWidth, 30);
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

        BuildFileMenu(store, visuals, refs.FileButton);
        BuildBoneMenu(store, visuals, ref refs);
        return refs.Ui;
    }

    private static void BuildHeader(UiBuilder ui, Vector2 screen, ref Refs refs)
    {
        ui.Container(Vector2.Zero, new Vector2(screen.X - Margin * 2, TopHeight),
            spacing: 16, color: UiTheme.Panel, padding: 14);
        ui.Container(Vector2.Zero, new Vector2(300, TopHeight - 28),
            StackDirection.Vertical, spacing: 4, color: Clear,
            cross: StackCrossAlignment.Start);
        ui.Text("XAF ANIMATOR", new Vector2(290, 28), UiTheme.HeadingFontSize,
            UiTheme.Accent);
        ui.Text("skeleton / clips / rigid SVG groups", new Vector2(290, 18),
            UiTheme.SmallFontSize, UiTheme.TextDim);
        ui.End();
        refs.Ui.Status = ui.Text("", new Vector2(380, 24), UiTheme.FontSize,
            new Color("#a3e635"));
        refs.Ui.DirtyLabel = ui.Text("", new Vector2(110, 24), UiTheme.SmallFontSize,
            UiTheme.Accent);
        refs.Ui.PathField = TextInput(ui, new Vector2(PathWidth, 26));
        // As wide as its list, so opening the menu does not widen the header slot.
        refs.FileButton = ui.Button("FILE ▾", new Vector2(FileMenuWidth, 30));
        ui.End();
    }

    private static void BuildLeft(UiBuilder ui, float height, ref Refs refs)
    {
        ui.Panel(Vector2.Zero, new Vector2(LeftWidth, height),
            cross: StackCrossAlignment.Start, main: StackMainAlignment.Start);
        ui.Text("BONES", new Vector2(ListWidth, 20), UiTheme.FontSize, UiTheme.Accent);
        Row(ui, ("ADD BONE", ToolActionKind.AddBone), ("DELETE", ToolActionKind.DeleteBone));

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
            StackDirection.Vertical, color: new Color("#0f172a"), takesInput: true);
        refs.Ui.Preview = ui.Current;
        ui.End();
    }

    private static void BuildRight(UiBuilder ui, float height, ref Refs refs)
    {
        ui.Panel(Vector2.Zero, new Vector2(RightWidth, height),
            cross: StackCrossAlignment.Center, main: StackMainAlignment.Start);
        Row(ui, ("UNDO", ToolActionKind.Undo), ("REDO", ToolActionKind.Redo));
        Row(ui, ("PLAY", ToolActionKind.Play), ("STOP", ToolActionKind.Stop));
        ModeButton(ui, "AUTO KEY", AnimatorMode.AutoKey);
        ModeButton(ui, "ONION", AnimatorMode.Onion);
        ModeButton(ui, "REST POSE", AnimatorMode.Rest);
        ModeButton(ui, "SVG PLACE", AnimatorMode.Svg);
        Row(ui, ("ROT -", ToolActionKind.RotateLeft), ("ROT +", ToolActionKind.RotateRight));
        Row(ui, ("SCALE -", ToolActionKind.ScaleDown), ("SCALE +", ToolActionKind.ScaleUp));
        Action(ui, "KEY SELECTED", ToolActionKind.KeyBone, ButtonSize);
        Action(ui, "MOVABLE", ToolActionKind.ToggleMovable, ButtonSize);
        ui.Text("CLIPS", new Vector2(210, 20), UiTheme.FontSize, UiTheme.Accent);
        refs.Ui.ClipList = List(ui, ButtonWidth);
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
        HeaderButton(ui, "NEW", ToolActionKind.NewClip, 44);
        HeaderButton(ui, "COPY", ToolActionKind.DuplicateClip, 48);
        HeaderButton(ui, "DELETE", ToolActionKind.DeleteClip, 56);
        HeaderButton(ui, "LEN -", ToolActionKind.DurationDown, 48);
        HeaderButton(ui, "LEN +", ToolActionKind.DurationUp, 48);
        refs.Ui.LoopButton = HeaderButton(ui, "LOOP OFF", ToolActionKind.ToggleLoop, 76);
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
        Entity button = HeaderButton(ui, label, ToolActionKind.Interpolate, width);
        button.GetComponent<ToolAction>().Argument = preset.ToString();
    }

    private static Entity HeaderButton(UiBuilder ui, string label, ToolActionKind kind, float width)
    {
        Entity button = ui.Button(
            label,
            new Vector2(width, AnimatorTimelineModel.HeaderHeight),
            fontSize: UiTheme.SmallFontSize);
        button.AddComponent(new ToolAction(kind));
        return button;
    }

    /// <summary>The file menu floats above the panels, so it lives on its own layer.</summary>
    private static void BuildFileMenu(
        EntityStore store,
        IVisualRidFactory? visuals,
        Entity fileButton)
    {
        (string Label, ToolActionKind Kind)[] entries =
        {
            ("SAVE XAF", ToolActionKind.Save),
            ("LOAD XAF", ToolActionKind.Load),
            ("NEW SKELETON", ToolActionKind.NewSkeleton),
        };
        const float optionHeight = 30f;
        float listHeight = entries.Length *
            (optionHeight + UiTheme.DropdownOptionSpacing) + 4f;
        var listSize = new Vector2(FileMenuWidth, listHeight);

        UiBuilder menu = UiBuilder.Begin(store, visuals).Layer(MenuLayer);
        Entity list = menu.RectEntity(UiLayout.Hidden, listSize, UiTheme.Panel,
            takesInput: true);
        for (int i = 0; i < entries.Length; i++)
        {
            Entity option = menu.Button(entries[i].Label,
                new Vector2(168, optionHeight), position: UiLayout.Hidden);
            option.AddComponent(new ToolAction(entries[i].Kind));
            option.AddComponent(new DropdownOption(fileButton, i));
        }
        fileButton.AddComponent(new Dropdown(list, 0, listSize));
    }

    /// <summary>Size of the bone context menu opened by right-clicking a bone label.</summary>
    public static readonly Vector2 BoneMenuSize = new(150, 4 * 28 + 3 * 2 + 8);

    public static readonly Vector2 BoneNameSize = new(140, 22);

    /// <summary>
    /// The bone context menu and its rename field float above the panels and
    /// start hidden; the bone menu system moves them into place.
    /// </summary>
    private static void BuildBoneMenu(EntityStore store, IVisualRidFactory? visuals, ref Refs refs)
    {
        (string Label, ToolActionKind Kind)[] entries =
        {
            ("RENAME", ToolActionKind.RenameBone),
            ("REPARENT", ToolActionKind.ReparentBone),
            ("ADD CHILD", ToolActionKind.AddBone),
            ("DELETE", ToolActionKind.DeleteBone),
        };

        UiBuilder menu = UiBuilder.Begin(store, visuals).Layer(MenuLayer);
        menu.Container(UiLayout.Hidden, BoneMenuSize, StackDirection.Vertical,
            spacing: 2, color: UiTheme.Panel, padding: 4, takesInput: true);
        refs.Ui.BoneMenu = menu.Current;
        foreach ((string label, ToolActionKind kind) in entries)
        {
            menu.Button(label, new Vector2(BoneMenuSize.X - 8, 28),
                fontSize: UiTheme.SmallFontSize).AddComponent(new ToolAction(kind));
        }
        menu.End();

        refs.Ui.BoneNameField = TextInput(menu, BoneNameSize);
        refs.Ui.BoneNameField.GetComponent<Position>().Value = UiLayout.Hidden;
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

    private static Entity Action(UiBuilder ui, string label, ToolActionKind kind, Vector2 size)
    {
        Entity button = ui.Button(label, size);
        button.AddComponent(new ToolAction(kind));
        return button;
    }

    private static void ModeButton(UiBuilder ui, string label, AnimatorMode mode)
    {
        Entity button = ui.Button($"{label} OFF", ButtonSize);
        button.AddComponent(new Toggle($"{label} ON", $"{label} OFF", false));
        button.AddComponent(new ModeToggle(mode));
    }

    private struct Refs
    {
        public AnimatorUi Ui;
        public Entity FileButton;
    }
}

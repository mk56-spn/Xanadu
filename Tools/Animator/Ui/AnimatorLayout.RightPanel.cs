using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tools.Animator;

/// <summary>
/// The right panel. The list on top holds what the rig is made of, one
/// collapsible section per kind, each with its own add and delete; the tabs
/// below act on whatever is selected in it.
/// </summary>
public static partial class AnimatorLayout
{
    /// <summary>Width of a row in the rig list, inside its scrolling viewport.</summary>
    public const float EntryWidth = ListWidth - 10;

    private const float SectionRowHeight = UiTheme.TreeRowHeight;
    private const float CaptionHeight = 20f;

    /// <summary>The tab order; the selection system switches by these indices.</summary>
    public const int PoseTab = 0;
    public const int EffectTab = 1;
    public const int FlowTab = 2;
    public const int LayersTab = 3;
    private static readonly string[] TabNames = { "POSE", "EFFECT", "FLOW", "LAYERS" };

    /// <summary>Width of a row in the LAYERS tab, inside the tab's padding.</summary>
    public const float LayerRowWidth = RightPanelWidth - UiTheme.ContainerPadding * 2;

    /// <summary>The tree on top, then the tab strip, then room for the selected tab's body.</summary>
    private static void BuildRightPanel(UiBuilder ui, EditorShellRects shell, ref Refs refs)
    {
        ui.Container(Vector2.Zero, shell.RightPanel.Size, StackDirection.Vertical,
            color: UiTheme.Panel, main: StackMainAlignment.Start);
        refs.Ui.RightPanel = ui.Current;
        BuildRigList(ui, shell.RightTree.Size.Y, ref refs);

        ui.Container(Vector2.Zero, shell.RightTabs.Size, color: Clear);
        refs.TabStrip = ui.Current;
        refs.Ui.Tabs = ui.Current;
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

    /// <summary>
    /// Bones, attachments, chains and effects in one scrolling list, so a long
    /// rig never spills over the tabs. Each kind folds under its own header,
    /// which the hierarchy system titles with a count.
    /// </summary>
    private static void BuildRigList(UiBuilder ui, float height, ref Refs refs)
    {
        ui.Container(Vector2.Zero, new Vector2(RightPanelWidth, height),
            StackDirection.Vertical, color: UiTheme.Panel,
            padding: UiTheme.ContainerPadding / 2, main: StackMainAlignment.Start);
        ui.Container(Vector2.Zero, new Vector2(ListWidth, height - UiTheme.ContainerPadding),
            StackDirection.Vertical, clip: true, color: UiTheme.Panel, takesInput: false);
        Entity viewport = ui.Current;
        ui.Container(Vector2.Zero, new Vector2(ListWidth, 4), StackDirection.Vertical,
            spacing: UiTheme.ContainerSpacing, color: Clear,
            cross: StackCrossAlignment.Start, autoHeight: true,
            main: StackMainAlignment.Start);
        Entity content = ui.Current;

        refs.Ui.BoneSection = Section(ui, "SKELETON");
        refs.Ui.BoneList = ui.Tree(EntryWidth);
        ui.EndCollapsible();

        refs.Ui.AttachmentSection = Section(ui, "ATTACHMENTS");
        refs.Ui.AttachmentList = List(ui, EntryWidth);
        ui.EndCollapsible();

        refs.Ui.ChainSection = Section(ui, "CHAINS");
        refs.Ui.ChainList = List(ui, EntryWidth);
        ui.EndCollapsible();

        refs.Ui.EffectSection = Section(ui, "EFFECTS");
        SectionRow(ui, ("+ STREAK", ToolActionKind.AddEffect), ("+ PARTICLES", ToolActionKind.AddParticles),
            ("DELETE", ToolActionKind.DeleteEffect));
        refs.Ui.EffectList = List(ui, EntryWidth);
        ui.EndCollapsible();

        refs.Ui.ClothSection = Section(ui, "CLOTH");
        SectionRow(ui, ("+ DRESS", ToolActionKind.AddCloth), ("DELETE", ToolActionKind.DeleteCloth));
        refs.Ui.ClothList = List(ui, EntryWidth);
        ui.EndCollapsible();

        ui.End();
        ui.End();
        viewport.AddComponent(new ScrollView());
        ui.End();
    }

    /// <summary>Opens a collapsible section of the rig list; the body is left current.</summary>
    private static Entity Section(UiBuilder ui, string title)
    {
        ui.Collapsible(title, ListWidth, spacing: 4);
        return ui.Current.Parent;
    }

    /// <summary>A section's add/delete pair: a row as tall as a list row, in the small font.</summary>
    private static void SectionRow(UiBuilder ui, params (string Label, ToolActionKind Kind)[] buttons)
    {
        ui.Container(Vector2.Zero, new Vector2(EntryWidth, SectionRowHeight), spacing: 4, color: Clear);
        var size = new Vector2((EntryWidth - 4 * (buttons.Length - 1)) / buttons.Length, SectionRowHeight);
        foreach ((string label, ToolActionKind kind) in buttons)
            Action(ui, label, kind, size, UiTheme.SmallFontSize);
        ui.End();
    }

    /// <summary>
    /// The tab bodies float on the menu layer: a container in a stack cannot be
    /// hidden, so each body is parked off screen until its tab is selected.
    /// </summary>
    private static void BuildTabBodies(UiBuilder menu, EditorShellRects shell, ref Refs refs)
    {
        var bodies = new Entity[TabNames.Length];
        for (int i = 0; i < bodies.Length; i++)
        {
            menu.Container(Vector2.Zero, shell.RightBody.Size, StackDirection.Vertical,
                spacing: UiTheme.ContainerSpacing, color: UiTheme.Panel,
                padding: UiTheme.ContainerPadding, takesInput: true,
                cross: StackCrossAlignment.Center, main: StackMainAlignment.Start);
            UiLayout.SetHidden(menu.Current, true);
            bodies[i] = menu.Current;
            switch (i)
            {
                case PoseTab:
                    Entity modulate = default;
                    BuildScrolling(menu, shell.RightBody.Size, rows => modulate = BuildPoseBody(rows));
                    refs.Ui.ModulateField = modulate;
                    break;
                case EffectTab:
                    BuildEffectBody(menu, ref refs);
                    break;
                case FlowTab:
                    BuildFlowBody(menu, shell.RightBody.Size, ref refs);
                    break;
                default:
                    Entity layers = default;
                    BuildScrolling(menu, shell.RightBody.Size, rows => layers = List(rows, LayerRowWidth));
                    refs.Ui.LayerList = layers;
                    break;
            }
            menu.End();
        }

        refs.TabStrip.AddComponent(new UiTabs(
            refs.TabButtons,
            bodies,
            shell.RightBody.Position + shell.RightBody.Size / 2));
    }

    /// <summary>
    /// A body whose rows may outgrow the tab: they sit in a clipped viewport
    /// that scrolls, so they never spill over the timeline below.
    /// </summary>
    private static void BuildScrolling(UiBuilder menu, Vector2 body, System.Action<UiBuilder> rows)
    {
        float width = body.X - UiTheme.ContainerPadding * 2;
        menu.Container(Vector2.Zero, new Vector2(width, body.Y - UiTheme.ContainerPadding * 2),
            StackDirection.Vertical, clip: true, color: UiTheme.Panel, takesInput: false);
        Entity viewport = menu.Current;
        menu.Container(Vector2.Zero, new Vector2(width, 4), StackDirection.Vertical,
            spacing: UiTheme.ContainerSpacing, color: Clear,
            cross: StackCrossAlignment.Center, autoHeight: true,
            main: StackMainAlignment.Start);
        Entity content = menu.Current;
        rows(menu);
        menu.End();
        menu.End();
        viewport.AddComponent(new ScrollView());
    }

    /// <summary>
    /// The selected SVG, overrides, and keying and passing whole poses. All
    /// that is about one bone lives in the bone card under the inspector.
    /// </summary>
    private static Entity BuildPoseBody(UiBuilder menu)
    {
        // The selected SVG: what it shows, on which layer, and whether it bends.
        menu.Collapsible("ART", ButtonWidth);
        Subhead(menu, "SOURCE");
        SmallRow(menu, ("+ ART", ToolActionKind.AddArt), ("DROP ART", ToolActionKind.DropArt));
        Stepper(menu, "ART", ToolActionKind.PreviousArt, ToolActionKind.NextArt, "<", ">");
        SmallRow(menu, ("OPEN FILE", ToolActionKind.OpenArtFile));
        Subhead(menu, "DISPLAY");
        Entity modulate = UiNode.Wrap(menu.Current, menu.Visuals).ColorField(
            "COLOUR",
            ButtonWidth,
            "Self modulate of the selected SVG: its art is multiplied by this. Type #rrggbb or #rrggbbaa; leave blank for white.")
            .Entity;
        SmallRow(menu, ("SHOW / HIDE", ToolActionKind.ToggleArtVisible));
        Subhead(menu, "MESH");
        SmallRow(menu, ("MESH", ToolActionKind.ToggleMesh), ("BONES", ToolActionKind.PickMeshBones));
        Stepper(menu, "DETAIL", ToolActionKind.MeshDetailDown, ToolActionKind.MeshDetailUp);
        SliderRow(menu, "BLEND", AnimatorSliderValue.MeshBlend, 0f, Animation.XafMesh.MaxBlend, 0f, "0",
            "How wide a zone two bones share the mesh over, in pixels: wider bends softer");
        Subhead(menu, "WEIGHTS");
        SmallRow(menu, ("AUTO WEIGHTS", ToolActionKind.AutoWeights), ("PAINT", ToolActionKind.PaintWeights));
        SliderRow(menu, "BRUSH", AnimatorSliderValue.BrushSize,
            AnimatorWeightPaint.MinRadius, AnimatorWeightPaint.MaxRadius, 18f, "0",
            "Weight brush radius, in pixels");
        SliderRow(menu, "STRENGTH", AnimatorSliderValue.BrushStrength,
            AnimatorWeightPaint.MinStrength, AnimatorWeightPaint.MaxStrength, 0.08f, "0.00",
            "How hard each stroke paints weight on");
        menu.EndCollapsible();

        BuildTargetSection(menu);

        menu.Collapsible("KEYS", ButtonWidth);
        Subhead(menu, "KEY");
        SmallRow(menu, ("KEY POSE", ToolActionKind.KeyPose), ("+ EVENT", ToolActionKind.AddEvent));
        Subhead(menu, "WHOLE POSE");
        SmallRow(menu, ("COPY POSE", ToolActionKind.CopyPose), ("PASTE POSE", ToolActionKind.PastePose));
        SmallRow(menu, ("MIRROR", ToolActionKind.MirrorPose));
        Subhead(menu, "CLIPBOARD");
        SmallRow(menu, ("COPY KEYS", ToolActionKind.CopyKeys), ("PASTE KEYS", ToolActionKind.PasteKeys));
        menu.EndCollapsible();
        return modulate;
    }

    /// <summary>
    /// Keying the selected effect at the playhead. Its shape is edited in the
    /// XIF panel beside the inspector, which opens with the selection.
    /// </summary>
    private static void BuildEffectBody(UiBuilder menu, ref Refs refs)
    {
        refs.Ui.EffectCaption = menu.Text("", new Vector2(ButtonWidth, CaptionHeight),
            UiTheme.FontSize, UiTheme.Accent, TextAlign.Left);

        menu.Collapsible("KEY AT PLAYHEAD", ButtonWidth);
        SmallRow(menu, ("SHOW / HIDE", ToolActionKind.ToggleEffectVisible));
        SliderRow(menu, "OPACITY", AnimatorSliderValue.EffectOpacity, 0f, 100f, 100f, "0",
            "Key the selected effect's opacity, in percent, at the playhead");
        menu.EndCollapsible();

        menu.Text("Shape and fill: XIF panel, left of inspector",
            new Vector2(ButtonWidth, CaptionHeight), UiTheme.SmallFontSize,
            UiTheme.TextDim, TextAlign.Left);
    }
}

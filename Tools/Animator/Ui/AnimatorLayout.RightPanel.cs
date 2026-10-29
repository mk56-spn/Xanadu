using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;

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
    private static readonly string[] TabNames = { "POSE", "EFFECT", "FLOW" };

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
        SectionRow(ui, ("DELETE BONE", ToolActionKind.DeleteBone));
        refs.Ui.BoneList = ui.Tree(EntryWidth);
        ui.EndCollapsible();

        refs.Ui.AttachmentSection = Section(ui, "ATTACHMENTS");
        refs.Ui.AttachmentList = List(ui, EntryWidth);
        ui.EndCollapsible();

        refs.Ui.ChainSection = Section(ui, "CHAINS");
        refs.Ui.ChainList = List(ui, EntryWidth);
        ui.EndCollapsible();

        refs.Ui.EffectSection = Section(ui, "EFFECTS");
        SectionRow(ui, ("+ STREAK", ToolActionKind.AddEffect), ("DELETE", ToolActionKind.DeleteEffect));
        refs.Ui.EffectList = List(ui, EntryWidth);
        ui.EndCollapsible();

        refs.Ui.ClothSection = Section(ui, "CLOTH");
        SectionRow(ui, ("+ DRESS", ToolActionKind.AddCloth), ("DELETE", ToolActionKind.DeleteCloth));
        refs.Ui.ClothList = List(ui, EntryWidth);
        ui.EndCollapsible();

        ui.End();
        ui.End();
        viewport.AddComponent(new ScrollView(content));
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
            menu.Container(UiLayout.Hidden, shell.RightBody.Size, StackDirection.Vertical,
                spacing: UiTheme.ContainerSpacing, color: UiTheme.Panel,
                padding: UiTheme.ContainerPadding, takesInput: true,
                cross: StackCrossAlignment.Center, main: StackMainAlignment.Start);
            bodies[i] = menu.Current;
            switch (i)
            {
                case PoseTab:
                    BuildPoseBody(menu);
                    break;
                case EffectTab:
                    BuildEffectBody(menu, ref refs);
                    break;
                default:
                    BuildFlowBody(menu, shell.RightBody.Size, ref refs);
                    break;
            }
            menu.End();
        }

        refs.TabStrip.AddComponent(new UiTabs(
            refs.TabButtons,
            bodies,
            shell.RightBody.Position + shell.RightBody.Size / 2));
    }

    /// <summary>Moving the selected bones, then keying them and passing poses between frames.</summary>
    private static void BuildPoseBody(UiBuilder menu)
    {
        menu.Collapsible("TRANSFORM", ButtonWidth);
        Row(menu, ("ROT -", ToolActionKind.RotateLeft), ("ROT +", ToolActionKind.RotateRight));
        Row(menu, ("SCALE -", ToolActionKind.ScaleDown), ("SCALE +", ToolActionKind.ScaleUp));
        Row(menu, ("MIRROR", ToolActionKind.MirrorPose), ("MOVE", ToolActionKind.ToggleMovable));
        menu.EndCollapsible();

        menu.Collapsible("KEYS", ButtonWidth);
        SmallRow(menu, ("KEY BONE", ToolActionKind.KeyBone), ("KEY POSE", ToolActionKind.KeyPose));
        SmallRow(menu, ("COPY POSE", ToolActionKind.CopyPose), ("PASTE POSE", ToolActionKind.PastePose));
        SmallRow(menu, ("COPY KEYS", ToolActionKind.CopyKeys), ("PASTE KEYS", ToolActionKind.PasteKeys));
        menu.EndCollapsible();
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
        SmallRow(menu, ("FADE -", ToolActionKind.EffectOpacityDown), ("FADE +", ToolActionKind.EffectOpacityUp));
        menu.EndCollapsible();

        menu.Text("Shape and fill: XIF panel, left of inspector",
            new Vector2(ButtonWidth, CaptionHeight), UiTheme.SmallFontSize,
            UiTheme.TextDim, TextAlign.Left);
    }
}

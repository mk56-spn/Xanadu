using Friflo.Engine.ECS;
using Xanadu.Core.Keys;
using Xanadu.Core.Maths;
using Xanadu.Model.Animator;
using Xanadu.Model.Animator.Intents;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Screens.Animator;

/// <summary>
/// The side panels. The right one is the tree of what the rig is made of:
/// bones, with the parts, chains and cloth that hang off each under it, each
/// kind in its own colour, with the whole height to itself. The left one,
/// beside the tool strip, holds the tabs: SELECTED holds the card of whatever
/// is selected (nothing floats over the preview any more); KEYS, FLOW and
/// LAYERS do not depend on the selection.
/// </summary>
public static partial class AnimatorLayout
{
    /// <summary>Width of a row in the rig list, inside its scrolling viewport.</summary>
    public const float EntryWidth = ListWidth - 10;

    /// <summary>Row height of the rig list (tree rows, section headers, entries): slimmer than the theme's, so the list stays light.</summary>
    public const float RigRowHeight = 20f;

    private const float SectionRowHeight = RigRowHeight;
    private const float CaptionHeight = 20f;

    /// <summary>The tab order; the selection system switches by these indices.</summary>
    public const int SelectedTab = 0;
    public const int KeysTab = 1;
    public const int FlowTab = 2;
    public const int LayersTab = 3;
    private static readonly string[] TabNames = ["SELECTED", "KEYS", "FLOW", "LAYERS"];

    /// <summary>Width of a row in the LAYERS tab, inside the tab's padding.</summary>
    public const float LayerRowWidth = RightPanelWidth - UiTheme.ContainerPadding * 2;

    /// <summary>The rig tree, the whole panel high.</summary>
    private static void BuildRightPanel(UiBuilder ui, EditorShellRects shell, ref Refs refs)
    {
        ui.Container(Vector2.Zero, shell.RightPanel.Size, StackDirection.Vertical,
            color: UiTheme.Panel, main: StackMainAlignment.Start);
        refs.Ui.RightPanel = ui.Current;
        BuildRigList(ui, shell.Tree.Size.Y, ref refs);
        ui.End();
    }

    /// <summary>The tab strip, then room for the selected tab's body, which floats over it.</summary>
    private static void BuildPropertiesPanel(UiBuilder ui, EditorShellRects shell, ref Refs refs)
    {
        ui.Container(Vector2.Zero, shell.LeftPanel.Size, StackDirection.Vertical,
            color: UiTheme.Panel, main: StackMainAlignment.Start);
        refs.Ui.PropertiesPanel = ui.Current;

        ui.Container(Vector2.Zero, shell.Tabs.Size, color: Clear);
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

        ui.RectEntity(Vector2.Zero, shell.Properties.Size, Clear);
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

        refs.Ui.BoneSection = Section(ui, "RIG");
        refs.Ui.BoneList = ui.Tree(EntryWidth, RigRowHeight);
        ui.EndCollapsible();

        ui.End();
        ui.End();
        viewport.AddComponent(new ScrollOffset());
        ui.End();
    }

    /// <summary>Opens a collapsible section of the rig list; the body is left current.</summary>
    private static Entity Section(UiBuilder ui, string title)
    {
        ui.Collapsible(title, ListWidth, spacing: 2, headerHeight: RigRowHeight);
        return ui.Current.Parent;
    }

    /// <summary>A section's add/delete pair: a row as tall as a list row, in the small font.</summary>
    private static void SectionRow(UiBuilder ui, params (string Label, IIntent Kind)[] buttons)
    {
        ui.Container(Vector2.Zero, new Vector2(EntryWidth, SectionRowHeight), spacing: 4, color: Clear);
        var size = new Vector2((EntryWidth - 4 * (buttons.Length - 1)) / buttons.Length, SectionRowHeight);
        foreach ((string label, IIntent kind) in buttons)
            Action(ui, label, kind, size, UiTheme.SmallFontSize);
        ui.End();
    }

    /// <summary>
    /// The tab bodies float on the menu layer, each parked off screen until
    /// its tab is selected.
    /// </summary>
    private static void BuildTabBodies(UiBuilder menu, EditorShellRects shell, ref Refs refs)
    {
        var bodies = new Entity[TabNames.Length];
        for (int i = 0; i < bodies.Length; i++)
        {
            menu.Container(Vector2.Zero, shell.Properties.Size, StackDirection.Vertical,
                spacing: UiTheme.ContainerSpacing, color: UiTheme.Panel,
                padding: UiTheme.ContainerPadding, takesInput: true,
                cross: StackCrossAlignment.Center, main: StackMainAlignment.Start);
            UiLayout.SetHidden(menu.Current, true);
            bodies[i] = menu.Current;
            Entity viewport;
            switch (i)
            {
                case SelectedTab:
                    viewport = BeginScrolling(menu, shell.Properties.Size);
                    BuildSelectedBody(menu, ref refs);
                    EndScrolling(menu, viewport);
                    break;
                case KeysTab:
                    viewport = BeginScrolling(menu, shell.Properties.Size);
                    BuildKeysBody(menu);
                    EndScrolling(menu, viewport);
                    break;
                case FlowTab:
                    BuildFlowBody(menu, shell.Properties.Size, ref refs);
                    break;
                default:
                    viewport = BeginScrolling(menu, shell.Properties.Size);
                    refs.Ui.LayerList = List(menu, LayerRowWidth);
                    EndScrolling(menu, viewport);
                    break;
            }
            menu.End();
        }

        refs.TabStrip.AddComponent(new UiTabs(
            refs.TabButtons,
            bodies,
            shell.Properties.Position + shell.Properties.Size / 2));
    }

    /// <summary>
    /// Opens a body whose rows may outgrow the tab: they sit in a clipped
    /// viewport that scrolls, so they never spill over the timeline below.
    /// The rows go in until <see cref="EndScrolling"/>.
    /// </summary>
    private static Entity BeginScrolling(UiBuilder menu, Vector2 body)
    {
        float width = body.X - UiTheme.ContainerPadding * 2;
        menu.Container(Vector2.Zero, new Vector2(width, body.Y - UiTheme.ContainerPadding * 2),
            StackDirection.Vertical, clip: true, color: UiTheme.Panel, takesInput: false);
        Entity viewport = menu.Current;
        menu.Container(Vector2.Zero, new Vector2(width, 4), StackDirection.Vertical,
            spacing: UiTheme.ContainerSpacing, color: Clear,
            cross: StackCrossAlignment.Center, autoHeight: true,
            main: StackMainAlignment.Start);
        return viewport;
    }

    private static void EndScrolling(UiBuilder menu, Entity viewport)
    {
        menu.End();
        menu.End();
        viewport.AddComponent(new ScrollOffset());
    }

    /// <summary>
    /// The selection's cards, one column: the transform, then the bone's card,
    /// or the part's, or the chain's, cloth's or event's. Each card hides while
    /// it has nothing to show, and the column closes up round it.
    /// </summary>
    private static void BuildSelectedBody(UiBuilder menu, ref Refs refs)
    {
        BuildInspector(menu, ref refs);
        BuildBoneCard(menu, ref refs);
        refs.Ui.OverrideCard = BeginCard(menu);
        BuildTargetSection(menu);
        menu.End();
        refs.Ui.PartCard = BeginCard(menu);
        refs.Ui.ModulateField = BuildPartCard(menu, ref refs);
        menu.End();
        BuildChainInspector(menu, ref refs);
        BuildClothInspector(menu, ref refs);
        BuildEventInspector(menu, ref refs);
    }

    /// <summary>A column of the SELECTED tab that sizes to its rows and starts hidden.</summary>
    private static Entity BeginCard(UiBuilder menu)
    {
        menu.Container(Vector2.Zero, new Vector2(ButtonWidth, 4), StackDirection.Vertical,
            spacing: UiTheme.ContainerSpacing, color: Clear,
            cross: StackCrossAlignment.Center, autoHeight: true,
            main: StackMainAlignment.Start);
        UiLayout.SetHidden(menu.Current, true);
        return menu.Current;
    }

    /// <summary>
    /// The selected part: how it is keyed at the playhead, then (only while
    /// EDIT RIG is on) what it shows and, folded away, how it bends with the bones.
    /// </summary>
    private static Entity BuildPartCard(UiBuilder menu, ref Refs refs)
    {
        refs.Ui.EffectCaption = menu.Text("", new Vector2(ButtonWidth, CaptionHeight),
            UiTheme.SmallFontSize, UiTheme.Accent, TextAlign.Left);

        menu.Collapsible("KEY AT PLAYHEAD", ButtonWidth);
        refs.Ui.HiddenInRig = [.. refs.Ui.HiddenInRig ?? [], menu.Current.Parent];
        SmallRow(menu, ("SHOW / HIDE", new ToggleEffectVisible()));
        SliderRow(menu, "OPACITY", AnimatorSliderValue.EffectOpacity, 0f, 100f, 100f, "0",
            "Key the selected part's opacity, in percent, at the playhead");
        menu.EndCollapsible();

        refs.Ui.PartRigHint = RigHint(menu, ButtonWidth, "ART, COLOUR AND MESH: EDIT RIG");

        // Art and mesh change the rig, so they only show while EDIT RIG is on.
        menu.Container(Vector2.Zero, new Vector2(ButtonWidth, 4), StackDirection.Vertical,
            spacing: UiTheme.ContainerSpacing, color: Clear, cross: StackCrossAlignment.Center,
            autoHeight: true, main: StackMainAlignment.Start);
        refs.Ui.PartRigRows = menu.Current;
        menu.Collapsible("ART", ButtonWidth);
        SmallRow(menu, ("EDIT XIF", new EditXif()));
        SmallRow(menu, ("+ ART", new AddArt()), ("DROP ART", new DropArt()));
        Stepper(menu, "ART", new StepArt(-1), new StepArt(1), "<", ">");
        Entity modulate = Field(menu, "COLOUR", new AnimatorValueBinding(AnimatorFieldKind.PartColour),
            UiValueShape.ColourValue, [1f, 1f, 1f, 1f], null,
            "Self modulate of the selected part: its art is multiplied by this", ButtonWidth);
        menu.EndCollapsible();

        menu.Collapsible("MESH", ButtonWidth, expanded: false);
        SmallRow(menu, ("MESH", new ToggleMesh()), ("BONES", new PickMeshBones()));
        Stepper(menu, "DETAIL", new StepMeshDetail(-1), new StepMeshDetail(1));
        SliderRow(menu, "BLEND", AnimatorSliderValue.MeshBlend, 0f, Xanadu.Model.Animation.RigMesh.MaxBlend, 0f, "0",
            "How wide a zone two bones share the mesh over, in pixels: wider bends softer");
        Subhead(menu, "WEIGHTS");
        SmallRow(menu, ("AUTO WEIGHTS", new AutoWeights()), ("PAINT", new PaintWeights()));
        SliderRow(menu, "BRUSH", AnimatorSliderValue.BrushSize,
            AnimatorWeightPaint.MinRadius, AnimatorWeightPaint.MaxRadius, 18f, "0",
            "Weight brush radius, in pixels");
        SliderRow(menu, "STRENGTH", AnimatorSliderValue.BrushStrength,
            AnimatorWeightPaint.MinStrength, AnimatorWeightPaint.MaxStrength, 0.08f, "0.00",
            "How hard each stroke paints weight on");
        menu.EndCollapsible();
        menu.End();
        return modulate;
    }

    /// <summary>Keying and passing whole poses: nothing here depends on what is selected.</summary>
    private static void BuildKeysBody(UiBuilder menu)
    {
        Subhead(menu, "KEY");
        SmallRow(menu, ("KEY POSE", new KeyPose()), ("+ EVENT", new AddEvent()));
        Subhead(menu, "WHOLE POSE");
        SmallRow(menu, ("COPY POSE", new CopyPose()), ("PASTE POSE", new PastePose()));
        SmallRow(menu, ("MIRROR", new MirrorPose()));
        Subhead(menu, "CLIPBOARD");
        SmallRow(menu, ("COPY KEYS", new CopyKeys()), ("PASTE KEYS", new PasteKeys()));
    }
}

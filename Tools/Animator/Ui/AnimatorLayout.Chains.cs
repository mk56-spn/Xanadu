using Friflo.Engine.ECS;
using Xanadu.Tools.Animator.Ecs;
using Xanadu.Ui.Ecs.Ui;
using ChainSetting = Xanadu.Tools.Animator.Commands.AnimatorChainCommands.Setting;

namespace Xanadu.Tools.Animator.Ui;

/// <summary>
/// The chain inspector: everything about the selected Verlet chain. It takes
/// the transform inspector's spot while a chain is selected, since that
/// inspector has nothing to show for one.
/// </summary>
public static partial class AnimatorLayout
{
    private const float ChainInspectorWidth = InspectorWidth;
    private const float ChainRow = 24f;
    private const float ChainSpacing = 4f;
    private const float ChainLabelWidth = 64f;

    /// <summary>
    /// Rows under the title: art, bone, offset, six settings, collider bone,
    /// collider offset and collider radius. Deleting is the rig tree's (right-click the row).
    /// </summary>
    private const int ChainRows = 14;

    public static readonly Vector2 ChainInspectorSize =
        new(ChainInspectorWidth, 8 * 2 + 18 + ChainRows * ChainRow + ChainRows * ChainSpacing);

    /// <summary>Where the chain inspector sits when shown; it has to end above the timeline, which a layout test checks.</summary>
    public static Vector2 ChainInspectorTopLeft(Vector2 screen) => InspectorTopLeft(screen);

    /// <summary>Where the art picker opens for a chain: left of the inspector, level with its first rows.</summary>
    public static Vector2 ChainPickerAnchor(Vector2 screen) =>
        InspectorTopLeft(screen) + new Vector2(-PickerWidth - 8f, 24f);

    /// <summary>
    /// Starts hidden; the chain inspector system moves it into place while a
    /// chain is selected.
    /// </summary>
    private static void BuildChainInspector(UiBuilder menu, ref Refs refs)
    {
        const float inner = ChainInspectorWidth - 16f;
        menu.Container(Vector2.Zero, ChainInspectorSize, StackDirection.Vertical,
            spacing: ChainSpacing, color: UiTheme.Panel, padding: 8, takesInput: true,
            cross: StackCrossAlignment.Start, autoHeight: true, main: StackMainAlignment.Start);
        UiLayout.SetHidden(menu.Current, true);
        refs.Ui.ChainInspector = menu.Current;
        refs.Ui.ChainTitle = menu.Text("NO CHAIN",
            new Vector2(inner, 18), UiTheme.SmallFontSize, UiTheme.Accent, TextAlign.Left);

        refs.Ui.ChainBoneButton = ChainButton(menu, "BONE", ToolActionKind.PickChainBone, inner);
        refs.Ui.ChainSheetButton = ChainButton(menu, "DRAW", ToolActionKind.ToggleChainSheet, inner);
        refs.Ui.ChainOffsetX = ChainPair(menu, "OFFSET", inner,
            "Where the chain starts, in its bone's own space; or drag the dot in the preview",
            AnimatorFieldKind.Chain, (int)ChainSetting.OffsetX, (int)ChainSetting.OffsetY);

        refs.Ui.ChainSegments = ChainField(menu, "SEGS", inner,
            "How many links the chain has", AnimatorFieldKind.Chain, (int)ChainSetting.Segments, whole: true);
        refs.Ui.ChainLength = ChainField(menu, "LENGTH", inner,
            "Length of each link, in pixels", AnimatorFieldKind.Chain, (int)ChainSetting.Length);
        refs.Ui.ChainWidth = ChainField(menu, "WIDTH", inner,
            "Width of the art at the root; it narrows towards the tip", AnimatorFieldKind.Chain, (int)ChainSetting.Width);
        refs.Ui.ChainLayer = ChainField(menu, "LAYER", inner,
            "Draw layer of the chain's art. Hair one below the head tucks under it", AnimatorFieldKind.Chain, (int)ChainSetting.Layer, whole: true);
        refs.Ui.ChainGravity = ChainField(menu, "GRAVITY", inner,
            "Downward pull, in pixels per second squared", AnimatorFieldKind.Chain, (int)ChainSetting.Gravity);
        refs.Ui.ChainLean = ChainField(menu, "LEAN", inner,
            "Sideways pull towards the way the character faces; sends the chain over its collider", AnimatorFieldKind.Chain, (int)ChainSetting.Lean);
        refs.Ui.ChainDamping = ChainField(menu, "DAMP", inner,
            "Share of speed kept each step, 0 to 1; lower settles sooner", AnimatorFieldKind.Chain, (int)ChainSetting.Damping, range: (0f, 1f));

        refs.Ui.ChainColliderButton = ChainButton(menu, "HIT BONE", ToolActionKind.PickColliderBone, inner);
        refs.Ui.ChainColliderX = ChainPair(menu, "HIT AT", inner,
            "Where the collider's centre is, in its bone's own space; or drag its ring in the preview",
            AnimatorFieldKind.Chain, (int)ChainSetting.ColliderX, (int)ChainSetting.ColliderY);
        refs.Ui.ChainCollide = ChainField(menu, "HIT SIZE", inner,
            "Radius of the circle the chain cannot enter, such as a shoulder; 0 for none", AnimatorFieldKind.Chain, (int)ChainSetting.Collide);
        menu.End();
    }

    /// <summary>A labelled button that runs an action; the chain inspector system writes its current value on it.</summary>
    private static Entity ChainButton(UiBuilder menu, string label, ToolActionKind kind, float width)
    {
        menu.Container(Vector2.Zero, new Vector2(width, ChainRow), spacing: 4, color: Clear);
        menu.Text(label, new Vector2(ChainLabelWidth, 20), 11, UiTheme.TextDim, TextAlign.Left);
        Entity button = menu.Button("", new Vector2(width - ChainLabelWidth - 4, 22),
            fontSize: UiTheme.SmallFontSize);
        button.AddComponent(ToolAction.Of(kind));
        AnimatorTooltips.Attach(button, kind);
        menu.End();
        return button;
    }

    /// <summary>Two numbers of a chain or cloth as one value field (x, y), bound to two of its settings.</summary>
    private static Entity ChainPair(
        UiBuilder menu,
        string label,
        float width,
        string tooltip,
        AnimatorFieldKind kind,
        int x,
        int y,
        bool whole = false) =>
        Field(menu, label, new AnimatorValueBinding(kind, x, y),
            UiValueShape.Vector(2, whole ? UiScalar.Int : UiScalar.Float), [0f, 0f], null, tooltip, width);

    /// <summary>One number of a chain or cloth as a value field, bound to that setting.</summary>
    private static Entity ChainField(
        UiBuilder menu,
        string label,
        float width,
        string tooltip,
        AnimatorFieldKind kind,
        int setting,
        bool whole = false,
        (float Min, float Max)? range = null) =>
        Field(menu, label, new AnimatorValueBinding(kind, setting),
            whole ? UiValueShape.Int : UiValueShape.Float, [0f], range, tooltip, width);
}

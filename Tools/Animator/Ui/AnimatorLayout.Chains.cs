using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;

namespace Xanadu.Tools.Animator;

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
    /// collider offset, collider radius and delete.
    /// </summary>
    private const int ChainRows = 13;

    public static readonly Vector2 ChainInspectorSize =
        new(ChainInspectorWidth, 8 * 2 + 18 + ChainRows * ChainRow + ChainRows * ChainSpacing);

    /// <summary>Where the chain inspector sits when shown; it has to end above the timeline, which a layout test checks.</summary>
    public static Vector2 ChainInspectorTopLeft(Vector2 screen) => InspectorTopLeft(screen);

    /// <summary>Where the art picker opens for a chain: left of the inspector, level with its first rows.</summary>
    public static Vector2 ChainPickerAnchor(Vector2 screen) =>
        InspectorTopLeft(screen) + new Vector2(-SvgMenuWidth - 8f, 24f);

    /// <summary>
    /// Starts hidden; the chain inspector system moves it into place while a
    /// chain is selected.
    /// </summary>
    private static void BuildChainInspector(UiBuilder menu, ref Refs refs)
    {
        const float inner = ChainInspectorWidth - 16f;
        menu.Container(UiLayout.Hidden, ChainInspectorSize, StackDirection.Vertical,
            spacing: ChainSpacing, color: UiTheme.Panel, padding: 8, takesInput: true,
            cross: StackCrossAlignment.Start, main: StackMainAlignment.Start);
        refs.Ui.ChainInspector = menu.Current;
        refs.Ui.ChainTitle = menu.Text("NO CHAIN",
            new Vector2(inner, 18), UiTheme.SmallFontSize, UiTheme.Accent, TextAlign.Left);

        refs.Ui.ChainArtButton = ChainButton(menu, "ART", ToolActionKind.PickChainArt, inner);
        refs.Ui.ChainBoneButton = ChainButton(menu, "BONE", ToolActionKind.PickChainBone, inner);
        ChainPair(menu, "OFFSET", inner,
            "Where the chain starts, in its bone's own space; or drag the dot in the preview",
            out refs.Ui.ChainOffsetX, out refs.Ui.ChainOffsetY);

        refs.Ui.ChainSegments = ChainField(menu, "SEGS", inner,
            "How many links the chain has");
        refs.Ui.ChainLength = ChainField(menu, "LENGTH", inner,
            "Length of each link, in pixels");
        refs.Ui.ChainWidth = ChainField(menu, "WIDTH", inner,
            "Width of the art at the root; it narrows towards the tip");
        refs.Ui.ChainGravity = ChainField(menu, "GRAVITY", inner,
            "Downward pull, in pixels per second squared");
        refs.Ui.ChainLean = ChainField(menu, "LEAN", inner,
            "Sideways pull towards the way the character faces; sends the chain over its collider");
        refs.Ui.ChainDamping = ChainField(menu, "DAMP", inner,
            "Share of speed kept each step, 0 to 1; lower settles sooner");

        refs.Ui.ChainColliderButton = ChainButton(menu, "HIT BONE", ToolActionKind.PickColliderBone, inner);
        ChainPair(menu, "HIT AT", inner,
            "Where the collider's centre is, in its bone's own space; or drag its ring in the preview",
            out refs.Ui.ChainColliderX, out refs.Ui.ChainColliderY);
        refs.Ui.ChainCollide = ChainField(menu, "HIT SIZE", inner,
            "Radius of the circle the chain cannot enter, such as a shoulder; 0 for none");

        Entity delete = menu.Button("DELETE CHAIN", new Vector2(inner, ChainRow),
            fontSize: UiTheme.SmallFontSize);
        delete.AddComponent(new ToolAction(ToolActionKind.DeleteChain));
        AnimatorTooltips.Attach(delete, ToolActionKind.DeleteChain);
        menu.End();
    }

    /// <summary>A labelled button that runs an action; the chain inspector system writes its current value on it.</summary>
    private static Entity ChainButton(UiBuilder menu, string label, ToolActionKind kind, float width)
    {
        menu.Container(Vector2.Zero, new Vector2(width, ChainRow), spacing: 4, color: Clear);
        menu.Text(label, new Vector2(ChainLabelWidth, 20), 11, UiTheme.TextDim, TextAlign.Left);
        Entity button = menu.Button("", new Vector2(width - ChainLabelWidth - 4, 22),
            fontSize: UiTheme.SmallFontSize);
        button.AddComponent(new ToolAction(kind));
        AnimatorTooltips.Attach(button, kind);
        menu.End();
        return button;
    }

    private static void ChainPair(
        UiBuilder menu,
        string label,
        float width,
        string tooltip,
        out Entity x,
        out Entity y)
    {
        menu.Container(Vector2.Zero, new Vector2(width, ChainRow), spacing: 4, color: Clear);
        menu.Text(label, new Vector2(ChainLabelWidth, 20), 11, UiTheme.TextDim, TextAlign.Left);
        x = NumericField(menu, new Vector2(72, 22));
        y = NumericField(menu, new Vector2(72, 22));
        x.AddComponent(new UiTooltip(tooltip));
        y.AddComponent(new UiTooltip(tooltip));
        menu.End();
    }

    private static Entity ChainField(UiBuilder menu, string label, float width, string tooltip)
    {
        menu.Container(Vector2.Zero, new Vector2(width, ChainRow), spacing: 4, color: Clear);
        menu.Text(label, new Vector2(ChainLabelWidth, 20), 11, UiTheme.TextDim, TextAlign.Left);
        Entity field = NumericField(menu, new Vector2(72, 22));
        field.AddComponent(new UiTooltip(tooltip));
        menu.End();
        return field;
    }
}

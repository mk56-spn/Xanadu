using Friflo.Engine.ECS;
using Xanadu.Tools.Animator.Ecs;
using Xanadu.Ui.Ecs.Ui;
using Setting = Xanadu.Tools.Animator.Commands.AnimatorClothCommands.Setting;

namespace Xanadu.Tools.Animator.Ui;

/// <summary>
/// The cloth inspector: everything about the selected cloth. Like the chain
/// inspector, it takes the transform inspector's spot while a cloth is
/// selected, and its rows share the chain inspector's labels and fields.
/// </summary>
public static partial class AnimatorLayout
{
    /// <summary>
    /// Rows under the title: bone, offset, grid, widths, length, gravity, lean,
    /// damping, stiffness, layer, art (EDIT XIF on its node), pins, colliders, collider size.
    /// Deleting is the rig tree's (right-click the row).
    /// </summary>
    private const int ClothRows = 15;

    public static readonly Vector2 ClothInspectorSize =
        new(ChainInspectorWidth, 8 * 2 + 18 + ClothRows * ChainRow + ClothRows * ChainSpacing);

    /// <summary>Starts hidden; the cloth inspector system moves it into place while a cloth is selected.</summary>
    private static void BuildClothInspector(UiBuilder menu, ref Refs refs)
    {
        const float inner = ChainInspectorWidth - 16f;
        menu.Container(Vector2.Zero, ClothInspectorSize, StackDirection.Vertical,
            spacing: ChainSpacing, color: UiTheme.Panel, padding: 8, takesInput: true,
            cross: StackCrossAlignment.Start, autoHeight: true, main: StackMainAlignment.Start);
        UiLayout.SetHidden(menu.Current, true);
        refs.Ui.ClothInspector = menu.Current;
        refs.Ui.ClothTitle = menu.Text("NO CLOTH",
            new Vector2(inner, 18), UiTheme.SmallFontSize, UiTheme.Accent, TextAlign.Left);

        var fields = new Entity[System.Enum.GetValues<Setting>().Length];
        refs.Ui.ClothBoneButton = ChainButton(menu, "BONE", ToolActionKind.PickClothBone, inner);
        fields[(int)Setting.OffsetX] = ChainPair(menu, "OFFSET", inner,
            "The centre of the top edge, in its bone's own space",
            AnimatorFieldKind.Cloth, (int)Setting.OffsetX, (int)Setting.OffsetY);
        fields[(int)Setting.Columns] = ChainPair(menu, "GRID", inner,
            "Points across, and rows from the top edge to the hem; pins and tethers on rows that go are dropped",
            AnimatorFieldKind.Cloth, (int)Setting.Columns, (int)Setting.Rows, whole: true);
        fields[(int)Setting.TopWidth] = ChainPair(menu, "WIDTH", inner,
            "Width at the top edge, and at the hem; the rows between flare evenly",
            AnimatorFieldKind.Cloth, (int)Setting.TopWidth, (int)Setting.HemWidth);
        fields[(int)Setting.Length] = ChainField(menu, "LENGTH", inner,
            "From the top edge to the hem, at rest", AnimatorFieldKind.Cloth, (int)Setting.Length);
        fields[(int)Setting.Gravity] = ChainField(menu, "GRAVITY", inner,
            "Downward pull, in pixels per second squared", AnimatorFieldKind.Cloth, (int)Setting.Gravity);
        fields[(int)Setting.Lean] = ChainField(menu, "LEAN", inner,
            "Sideways pull towards the way the character faces, like a breeze", AnimatorFieldKind.Cloth, (int)Setting.Lean);
        fields[(int)Setting.Damping] = ChainField(menu, "DAMP", inner,
            "Share of speed kept each step, 0 to 1; lower settles sooner", AnimatorFieldKind.Cloth, (int)Setting.Damping, range: (0f, 1f));
        fields[(int)Setting.Stiffness] = ChainField(menu, "STIFF", inner,
            "How hard it resists folding, 0 to 1: low drapes like silk, high holds out like felt", AnimatorFieldKind.Cloth, (int)Setting.Stiffness, range: (0f, 1f));
        fields[(int)Setting.Layer] = ChainField(menu, "LAYER", inner,
            "Draw layer among the attachments; higher draws in front", AnimatorFieldKind.Cloth, (int)Setting.Layer, whole: true);

        refs.Ui.ClothArtButton = ChainButton(menu, "ART", ToolActionKind.EditXif, inner);

        refs.Ui.ClothPinsButton = ChainButton(menu, "PINS", ToolActionKind.PinClothPoints, inner);
        fields[(int)Setting.TetherReach] = ChainField(menu, "REACH", inner,
            "How far every tethered point may swing from its spot on its bone", AnimatorFieldKind.Cloth, (int)Setting.TetherReach);
        refs.Ui.ClothCollidersButton = ChainButton(menu, "HITS", ToolActionKind.PickClothColliders, inner);
        fields[(int)Setting.HitSize] = ChainField(menu, "HIT SIZE", inner,
            "Radius of every collider: about the thickness of a leg", AnimatorFieldKind.Cloth, (int)Setting.HitSize);
        refs.Ui.ClothFields = fields;
        menu.End();
    }
}

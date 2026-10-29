using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Setting = Xanadu.Tools.Animator.Commands.AnimatorClothCommands.Setting;

namespace Xanadu.Tools.Animator;

/// <summary>
/// The cloth inspector: everything about the selected cloth. Like the chain
/// inspector, it takes the transform inspector's spot while a cloth is
/// selected, and its rows share the chain inspector's labels and fields.
/// </summary>
public static partial class AnimatorLayout
{
    /// <summary>
    /// Rows under the title: bone, offset, grid, widths, length, gravity, lean,
    /// damping, stiffness, layer, colour, pins, colliders, collider size, delete.
    /// </summary>
    private const int ClothRows = 15;

    public static readonly Vector2 ClothInspectorSize =
        new(ChainInspectorWidth, 8 * 2 + 18 + ClothRows * ChainRow + ClothRows * ChainSpacing);

    /// <summary>Starts hidden; the cloth inspector system moves it into place while a cloth is selected.</summary>
    private static void BuildClothInspector(UiBuilder menu, ref Refs refs)
    {
        const float inner = ChainInspectorWidth - 16f;
        menu.Container(UiLayout.Hidden, ClothInspectorSize, StackDirection.Vertical,
            spacing: ChainSpacing, color: UiTheme.Panel, padding: 8, takesInput: true,
            cross: StackCrossAlignment.Start, main: StackMainAlignment.Start);
        refs.Ui.ClothInspector = menu.Current;
        refs.Ui.ClothTitle = menu.Text("NO CLOTH",
            new Vector2(inner, 18), UiTheme.SmallFontSize, UiTheme.Accent, TextAlign.Left);

        var fields = new Entity[System.Enum.GetValues<Setting>().Length];
        refs.Ui.ClothBoneButton = ChainButton(menu, "BONE", ToolActionKind.PickClothBone, inner);
        ChainPair(menu, "OFFSET", inner,
            "The centre of the top edge, in its bone's own space",
            out fields[(int)Setting.OffsetX], out fields[(int)Setting.OffsetY]);
        ChainPair(menu, "GRID", inner,
            "Points across, and rows from the top edge to the hem; pins on rows that go are dropped",
            out fields[(int)Setting.Columns], out fields[(int)Setting.Rows]);
        ChainPair(menu, "WIDTH", inner,
            "Width at the top edge, and at the hem; the rows between flare evenly",
            out fields[(int)Setting.TopWidth], out fields[(int)Setting.HemWidth]);
        fields[(int)Setting.Length] = ChainField(menu, "LENGTH", inner,
            "From the top edge to the hem, at rest");
        fields[(int)Setting.Gravity] = ChainField(menu, "GRAVITY", inner,
            "Downward pull, in pixels per second squared");
        fields[(int)Setting.Lean] = ChainField(menu, "LEAN", inner,
            "Sideways pull towards the way the character faces, like a breeze");
        fields[(int)Setting.Damping] = ChainField(menu, "DAMP", inner,
            "Share of speed kept each step, 0 to 1; lower settles sooner");
        fields[(int)Setting.Stiffness] = ChainField(menu, "STIFF", inner,
            "How hard it resists folding, 0 to 1: low drapes like silk, high holds out like felt");
        fields[(int)Setting.Layer] = ChainField(menu, "LAYER", inner,
            "Draw layer among the attachments; higher draws in front");

        menu.Container(Vector2.Zero, new Vector2(inner, ChainRow), spacing: 4, color: Clear);
        menu.Text("COLOUR", new Vector2(ChainLabelWidth, 20), 11, UiTheme.TextDim, TextAlign.Left);
        refs.Ui.ClothFill = TextInput(menu, new Vector2(inner - ChainLabelWidth - 4, 22));
        refs.Ui.ClothFill.AddComponent(new UiTooltip("Fill colour as #rrggbb or #rrggbbaa; the hem shades a little darker"));
        menu.End();

        refs.Ui.ClothPinsButton = ChainButton(menu, "PINS", ToolActionKind.PinClothPoints, inner);
        refs.Ui.ClothCollidersButton = ChainButton(menu, "HITS", ToolActionKind.PickClothColliders, inner);
        fields[(int)Setting.HitSize] = ChainField(menu, "HIT SIZE", inner,
            "Radius of every collider: about the thickness of a leg");
        refs.Ui.ClothFields = fields;

        Entity delete = menu.Button("DELETE CLOTH", new Vector2(inner, ChainRow),
            fontSize: UiTheme.SmallFontSize);
        delete.AddComponent(new ToolAction(ToolActionKind.DeleteCloth));
        AnimatorTooltips.Attach(delete, ToolActionKind.DeleteCloth);
        menu.End();
    }
}

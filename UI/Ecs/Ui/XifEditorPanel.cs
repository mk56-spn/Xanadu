using Friflo.Engine.ECS;
using Godot;
using Xanadu.Theme;

namespace Xanadu.Ecs;

/// <summary>
/// Builds a XIF editor panel: the item's name, which primitive is being
/// edited, a shape box to drag its points in, and its fill, texture and
/// shader. Any tool can host one; <see cref="Systems.Ui.XifEditorSystem"/>
/// runs it and the host supplies the item through <see cref="IXifEditorHost"/>.
/// </summary>
public static class XifEditorPanel
{
    public const float Width = 260f;
    public static readonly Vector2 CanvasSize = new(Width - UiTheme.ContainerPadding * 2, 130f);
    private const float Row = 26f;
    private const float LabelWidth = 64f;
    private const float StepWidth = 30f;

    /// <summary>Total height, so a host can place the panel before it is laid out.</summary>
    public const float Height = 20 + Row * 7 + 130 + 16 + UiTheme.ContainerSpacing * 9 + UiTheme.ContainerPadding * 2;

    private static float Inner => Width - UiTheme.ContainerPadding * 2;

    /// <summary>
    /// Builds the panel parked off screen on <paramref name="layer"/>; it moves
    /// to <paramref name="home"/> (its centre) whenever the host has an item.
    /// </summary>
    public static Entity Build(UiNode layer, Vector2 home, IXifEditorHost host)
    {
        UiNode panel = layer.Panel().Size(Width, Height).At(UiLayout.Hidden)
            .Main(StackMainAlignment.Start);
        Entity editor = panel;
        var parts = new XifEditorParts();

        parts.Title = panel.Text("", UiType.Label, UiTheme.Accent).Size(Inner, 20);

        UiNode name = panel.Row().Size(Inner, Row).Gap(6);
        name.Text("NAME", UiType.Caption, UiTheme.TextDim).Size(LabelWidth, Row);
        parts.Name = Field(name, XifEditorFieldKind.Name, editor, Inner - LabelWidth - 6,
            "The item's name");

        UiNode primitives = panel.Row().Size(Inner, Row).Gap(4);
        Button(primitives, "<", XifEditorActionKind.PreviousPrimitive, editor, StepWidth, "Edit the previous primitive");
        parts.PrimitiveLabel = primitives.Text("", UiType.Caption, UiTheme.Text, TextAlign.Center)
            .Size(Inner - StepWidth * 2 - 8, Row);
        Button(primitives, ">", XifEditorActionKind.NextPrimitive, editor, StepWidth, "Edit the next primitive");

        parts.Canvas = panel.Rect(Palette.SurfaceInset, size: CanvasSize).Input()
            .Tooltip("Drag a point to move it; click one to select it");

        // Outlines float on the panel's layer over the box; the system places them.
        // Overlay puts them above the box, whose z stacks on the panel's.
        UiNode floating = panel.LayerNode;
        parts.Others = floating.Shape(Square(), new Color(0, 0, 0, 0), new Color(UiTheme.TextDim, 0.5f), 1f)
            .IgnoreLayout().Overlay().At(UiLayout.Hidden);
        parts.Shape = floating.Shape(Square(), new Color(Palette.Accent, 0.25f), Palette.Accent, 1.5f)
            .IgnoreLayout().Overlay().At(UiLayout.Hidden);
        parts.Origin = floating.Shape(Cross(), new Color(0, 0, 0, 0), Palette.Rose, 1.5f, closed: false)
            .IgnoreLayout().Overlay().At(UiLayout.Hidden);

        UiNode points = panel.Row().Size(Inner, Row).Gap(4);
        Button(points, "+ PT", XifEditorActionKind.InsertPoint, editor, 48, "Insert a point after the selected one");
        Button(points, "- PT", XifEditorActionKind.RemovePoint, editor, 48, "Delete the selected point; a polygon keeps three");
        parts.PointLabel = points.Text("", UiType.Caption, UiTheme.TextDim).Size(Inner - 104, Row);

        UiNode shapes = panel.Row().Size(Inner, Row).Gap(4);
        Button(shapes, "+ POLY", XifEditorActionKind.AddPolygon, editor, (Inner - 4) / 2, "Add a polygon to the item");
        Button(shapes, "DEL POLY", XifEditorActionKind.RemovePrimitive, editor, (Inner - 4) / 2,
            "Delete this primitive; the last one stays");

        UiNode fill = panel.Row().Size(Inner, Row).Gap(6);
        fill.Text("FILL", UiType.Caption, UiTheme.TextDim).Size(LabelWidth, Row);
        parts.Fill = Field(fill, XifEditorFieldKind.Fill, editor, Inner - LabelWidth - 6,
            "The polygon's colour as #rrggbb or #rrggbbaa");

        UiNode texture = panel.Row().Size(Inner, Row).Gap(6);
        texture.Text("TEXTURE", UiType.Caption, UiTheme.TextDim).Size(LabelWidth, Row);
        parts.Texture = Field(texture, XifEditorFieldKind.Texture, editor, Inner - LabelWidth - 6,
            "A texture stretched over the polygon, as a res:// path; empty for none");

        UiNode shader = panel.Row().Size(Inner, Row).Gap(6);
        shader.Text("SHADER", UiType.Caption, UiTheme.TextDim).Size(LabelWidth, Row);
        parts.Shader = Button(shader, "", XifEditorActionKind.NextShader, editor, Inner - LabelWidth - 6,
            "Draw the primitive with the next shader this tool allows");

        parts.Status = panel.Text("", UiType.Caption, UiTheme.TextDim).Size(Inner, 16);

        panel.With(new XifEditor
        {
            Host = host,
            Home = home,
            Primitive = 0,
            Point = 0,
            Parts = parts,
            Handles = new(),
            Status = "",
        });
        return editor;
    }

    private static Entity Button(
        UiNode row,
        string label,
        XifEditorActionKind kind,
        Entity editor,
        float width,
        string tooltip) =>
        row.Button(label, UiType.Caption).Size(width, Row)
            .With(new XifEditorAction(kind, editor))
            .Tooltip(tooltip);

    private static Entity Field(UiNode row, XifEditorFieldKind kind, Entity editor, float width, string tooltip) =>
        row.Rect(Palette.SurfaceInset, "", new Vector2(width, Row))
            .Input()
            .With(new TextStyle(UiType.Caption, UiTheme.Text))
            .Align(TextAlign.Left)
            .With(new TextField(""))
            .With(new XifEditorField(kind, editor))
            .Tooltip(tooltip);

    private static Vector2[] Square() => new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1) };

    /// <summary>A plus at the item's origin, where it attaches to whatever carries it.</summary>
    private static Vector2[] Cross() => new[]
    {
        new Vector2(-6, 0), new Vector2(6, 0), new Vector2(0, 0), new Vector2(0, -6), new Vector2(0, 6),
    };
}

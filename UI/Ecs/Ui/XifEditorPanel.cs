using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Xanadu.Core.Theme;

namespace Xanadu.Ui.Ecs.Ui;

/// <summary>
/// Builds a XIF editor: a panel that opens over the host's work, with a big
/// shape box on the left where the item is drawn filled and edited by hand
/// (drag points, click an edge to add one, right-click one to delete it,
/// click a shape to pick it, drag it to move it), and its settings on the
/// right: name, polygons, fill colour and shader. Any tool can host one;
/// <see cref="Xanadu.Ui.Systems.Ui.XifEditorSystem"/> runs it and the host
/// supplies the item through <see cref="IXifEditorHost"/>.
/// </summary>
public static class XifEditorPanel
{
    /// <summary>The settings column's width.</summary>
    public const float SideWidth = 250f;

    /// <summary>Room between the controls and the edge of their dark panel.</summary>
    private const float SidePad = 8f;

    private const float Row = 28f;

    /// <summary>How many shaders the open picker lists before it scrolls.</summary>
    private const int ShaderRows = 10;
    private const float Header = 30f;
    private const float StepWidth = 34f;

    /// <summary>The backgrounds at full opacity; the host's opacity scales their alpha.</summary>
    public static readonly Color BackdropColor = new(0f, 0f, 0f, 0.55f);
    public static readonly Color CanvasColor = Palette.SurfaceInset;

    /// <summary>A shape row: its colour swatch and its name button.</summary>
    public const float SwatchWidth = 28f;
    public const float ShapeRowHeight = 24f;

    /// <summary>How many shapes the box shows filled; more still edit, outlined only when picked.</summary>
    public const int MaxFilledShapes = 48;

    /// <summary>
    /// Builds the panel closed on <paramref name="layer"/>; opened, it covers
    /// <paramref name="area"/> (screen space), over a backdrop that dims all of
    /// <paramref name="screen"/> and takes its clicks. The field lines and fills
    /// draw on <paramref name="lines"/>, a layer of the host's above
    /// <paramref name="layer"/>, so the panel can fade while they stay solid;
    /// without one they share the panel's layer and fade with it.
    /// </summary>
    public static Entity Build(UiNode layer, Rect2 area, IXifEditorHost host, Vector2 screen, UiNode lines = default)
    {
        Entity backdrop = layer.Rect(BackdropColor, size: screen).Input()
            .IgnoreLayout().Hidden();

        float pad = UiTheme.ContainerPadding;
        float gap = UiTheme.ContainerSpacing;
        float inner = area.Size.X - pad * 2;
        float body = area.Size.Y - pad * 2 - Header - gap * 2 - 18;
        float canvasWidth = inner - SideWidth - SidePad * 2 - gap * 2;

        // One see-through backing across the whole editor, outlined, so what it
        // edits shows through while the editor still reads as one panel.
        UiNode panel = layer.Panel().Size(area.Size.X, area.Size.Y).Hidden()
            .Main(StackMainAlignment.Start).Backing().Outlined(2);
        Entity editor = panel;
        UiNode floating = lines.IsNull ? panel.LayerNode : lines;
        var parts = new XifEditorParts
        {
            Fills = [], Backdrop = backdrop, Screen = screen, Lines = floating,
        };

        UiNode header = panel.Row().Size(inner, Header).Gap(gap);
        parts.Title = header.Text("", UiType.Label, UiTheme.Accent).Size(inner - 120 - gap, Header);
        Button(header, "DONE  (ESC)", XifEditorActionKind.Done, editor, 120, "Close the editor; every change is already kept");

        UiNode main = panel.Row().Size(inner, body).Gap(gap * 2).Cross(StackCrossAlignment.Start);
        parts.Canvas = main.Rect(CanvasColor, size: new Vector2(canvasWidth, body)).Input()
            .Tooltip("Drag a point to move it · click an edge to add one · right-click a point to delete it · click a shape to pick it, drag it to move it · Shift: no snapping");

        UiNode side = main.Column().Size(SideWidth + SidePad * 2, body).Pad(SidePad).Gap(gap)
            .Main(StackMainAlignment.Start);

        Caption(side, "NAME");
        parts.Name = Field(side, XifEditorFieldKind.Name, editor, SideWidth, "The item's name");

        // One row per shape, back to front: its colour, then its name. The
        // system fills the list; the picker opens under it from a swatch.
        Caption(side, "SHAPES");
        parts.ShapeList = side.Column().Width(SideWidth).Gap(2).Cross(StackCrossAlignment.Start).Main(StackMainAlignment.Start).Entity;
        UiNode fill = side.Column().Width(SideWidth).Gap(4).Cross(StackCrossAlignment.Start).Main(StackMainAlignment.Start);
        parts.Fill = fill.ColorField("COLOUR", SideWidth, "The picked shape's colour: drag in the picker, or type #rrggbb or #rrggbbaa").Entity;
        parts.FillBox = fill.Entity;
        UiLayout.SetHidden(parts.FillBox, true);
        UiNode shapes = side.Row().Size(SideWidth, Row).Gap(4);
        float half = (SideWidth - 4) / 2;
        Button(shapes, "+ SHAPE", XifEditorActionKind.AddPolygon, editor, half, "Add a free shape on top: a square to drag into shape, point by point");
        Button(shapes, "+ CURVE", XifEditorActionKind.AddCurve, editor, half, "Add a curve: quadratic Bézier segments filled on the GPU; drag the on-curve points and the controls between them");
        UiNode primitives = side.Row().Size(SideWidth, Row).Gap(4);
        Button(primitives, "+ QUAD", XifEditorActionKind.AddQuad, editor, half, "Add a quad: a rectangle sized by dragging its corners");
        Button(primitives, "+ CIRCLE", XifEditorActionKind.AddCircle, editor, half, "Add a circle: an ellipse sized by dragging its four sides");
        Button(side, "DELETE SHAPE", XifEditorActionKind.RemovePolygon, editor, SideWidth,
            "Delete the picked shape; the last one stays");

        Caption(side, "POINT");
        UiNode points = side.Row().Size(SideWidth, Row).Gap(4);
        Button(points, "+ PT", XifEditorActionKind.InsertPoint, editor, 52, "Add a point after the picked one (or click an edge)");
        Button(points, "- PT", XifEditorActionKind.RemovePoint, editor, 52, "Delete the picked point (or right-click it); a shape keeps three");
        parts.PointLabel = points.Text("", UiType.Caption, UiTheme.TextDim).Size(SideWidth - 112, Row);

        Caption(side, "SHADER");
        parts.Shader = side.TabbedPicker(XifShaderTabs.Of(host.Shaders), SideWidth, rows: ShaderRows).Entity;
        UiTabbedPickers.ButtonOf(parts.Shader).AddComponent(new UiTooltip("Pick the shader the item draws with, by folder"));
        parts.Shader.OnPicked((tab, entry) => Systems.Ui.XifEditorShaders.PickShader(editor, tab, entry));

        // Shaders stacked over it, bottom first: each moves or colours what the one under it gave.
        parts.LayersCaption = side.Text("LAYERS", UiType.Caption, UiTheme.TextDim).Size(SideWidth, 16).Entity;
        parts.LayerList = side.Column().Width(SideWidth).Gap(2).Cross(StackCrossAlignment.Start).Main(StackMainAlignment.Start).Entity;
        IReadOnlyList<XifShaderChoice> layers = host.Layers;
        parts.LayerAdd = side.Dropdown(AddLayerText, layers.Select(layer => layer.Label).ToList(), 0, new Vector2(SideWidth, Row))
            .Button.Tooltip("Stack a shader over the item's: it moves or colours what the ones under it gave");
        parts.LayerAdd.OnPicked(pick => Systems.Ui.XifEditorShaders.AddLayer(editor, pick));
        UiLayout.SetHidden(parts.LayersCaption, layers.Count == 0);
        UiLayout.SetHidden(parts.LayerAdd, layers.Count == 0);
        parts.SettingsCaption = side.Text("SETTINGS", UiType.Caption, UiTheme.TextDim).Size(SideWidth, 16).Entity;
        parts.FitToggle = Button(side, "FIT TO SHAPE", XifEditorActionKind.ToggleFit, editor, SideWidth,
            "Size the shader to the item's shape: its fitted settings take their width and height from the shape");
        UiLayout.SetHidden(parts.FitToggle, true);
        parts.Settings = side.Column().Width(SideWidth).Gap(gap).Cross(StackCrossAlignment.Start).Main(StackMainAlignment.Start).Entity;

        parts.Status = panel.Text("", UiType.Caption, UiTheme.TextDim).Size(inner, 18);

        // Shapes float on the lines layer over the box; the system places them.
        // The fills come first so the outline, the marks and the handles draw over them.
        for (int i = 0; i < MaxFilledShapes; i++)
        {
            parts.Fills.Add(floating.Shape(Square(), Colors.White, new Color(0, 0, 0, 0), 1f)
                .IgnoreLayout().Overlay().Hidden());
        }
        parts.Shape = floating.Shape(Square(), new Color(0, 0, 0, 0), Palette.Accent, 2f)
            .IgnoreLayout().Overlay().Hidden();
        parts.Origin = floating.Shape(Cross(), new Color(0, 0, 0, 0), Palette.Rose, 1.5f, closed: false)
            .IgnoreLayout().Overlay().Hidden();
        parts.EdgeGhost = floating.Shape(Diamond(), Colors.White, Palette.Accent, 1.5f)
            .IgnoreLayout().Overlay().Hidden();

        panel.With(new XifEditor
        {
            Host = host,
            Home = area.GetCenter(),
            Polygon = 0,
            Point = 0,
            HoverPoint = -1,
            Parts = parts,
            Handles = [],
            Status = "",
            SettingsFor = "",
            LayersFor = "",
            ShapeRows = [],
            SettingFields = [],
        });
        return editor;
    }

    /// <summary>What the add-a-layer list's button always reads.</summary>
    public const string AddLayerText = "+ LAYER ▼";

    private static void Caption(UiNode column, string text) =>
        column.Text(text, UiType.Caption, UiTheme.TextDim).Size(SideWidth, 16);

    private static Entity Button(
        UiNode row,
        string label,
        XifEditorActionKind kind,
        Entity editor,
        float width,
        string tooltip) =>
        row.Button(label, UiType.Caption).Size(width, Row)
            .With(UiCommand.Of(kind, owner: editor))
            .Tooltip(tooltip);

    private static Entity Field(UiNode row, XifEditorFieldKind kind, Entity editor, float width, string tooltip) =>
        row.Rect(Palette.SurfaceInset, "", new Vector2(width, Row))
            .Input()
            .With(new TextStyle(UiType.Caption, UiTheme.Text))
            .Align(TextAlign.Left)
            .With(new TextField(""))
            .With(new XifEditorField(kind, editor))
            .Tooltip(tooltip);

    private static Vector2[] Square() => [new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1)];

    private static Vector2[] Diamond() => [new Vector2(0, -5), new Vector2(5, 0), new Vector2(0, 5), new Vector2(-5, 0)];

    /// <summary>A plus at the item's origin, where it attaches to whatever carries it.</summary>
    private static Vector2[] Cross() =>
    [
        new Vector2(-8, 0), new Vector2(8, 0), new Vector2(0, 0), new Vector2(0, -8), new Vector2(0, 8),
    ];
}

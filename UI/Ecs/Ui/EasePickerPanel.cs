using System;
using Friflo.Engine.ECS;
using Xanadu.Theme;
using Xanadu.Timeline;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Ecs;

/// <summary>
/// Builds the shared ease picker: a live preview of a curve at the left, and
/// every curve at the right as a thumbnail, one row per family and one column
/// per direction. Any tool can host one through <see cref="IEasePickerHost"/>;
/// <see cref="Systems.Ui.EasePickerSystem"/> runs it. The animator's keys and
/// the level editor's group effects and shader keys all pick from it.
/// </summary>
public static class EasePickerPanel
{
    /// <summary>The cells' fill; the selection's curve is lit with the selection colour.</summary>
    public static readonly Color Cell = Palette.SurfaceAlt;
    public static readonly Color Lit = Palette.Selected;

    /// <summary>The curves, in the thumbnails and the preview.</summary>
    public static readonly Color Stroke = Palette.Text;

    /// <summary>The 0 and 1 guide lines behind the preview curve.</summary>
    public static readonly Color Guide = Palette.WithAlpha(Palette.TextDim, 0.35f);

    /// <summary>The dots that run the preview curve.</summary>
    public static readonly Color DotColor = Palette.Accent;

    /// <summary>The live preview's box, inside which the system draws.</summary>
    public static readonly Vector2 PreviewSize = new(230f, 170f);

    /// <summary>How far in from the box's edges the curve keeps.</summary>
    public const float PreviewInset = 18f;

    /// <summary>Where the value rail sits, in from the box's right edge.</summary>
    public const float RailInset = 10f;

    public const float DotRadius = 5f;

    private const float TitleHeight = 24f;
    private const float RowLabelWidth = 62f;
    private const float ColumnLabelHeight = 18f;
    private const float Gap = 4f;
    private static readonly Vector2 CellSize = new(62f, 36f);
    private static readonly Vector2 ThumbSize = new(48f, 22f);
    private static readonly Color Clear = new(0, 0, 0, 0);

    private static float Pad => UiTheme.ContainerPadding;
    private static float Spacing => UiTheme.ContainerSpacing;

    private static float GridWidth => RowLabelWidth + (CellSize.X + Gap) * 3;

    private static float GridHeight =>
        ColumnLabelHeight + (CellSize.Y + Gap) * (EaseNames.Families.Length + 1);

    private static float Inner => Size.X - Pad * 2;

    /// <summary>The panel's whole size, so a host or the system can place it before it is laid out.</summary>
    public static Vector2 Size => new(
        Pad * 2 + PreviewSize.X + Spacing + GridWidth,
        Pad * 2 + TitleHeight + Spacing + GridHeight);

    /// <summary>Builds the picker hidden on <paramref name="layer"/>; it shows while the host says it is open.</summary>
    public static Entity Build(UiNode layer, IEasePickerHost host)
    {
        UiNode panel = layer.Panel().Size(Size).Hidden()
            .Cross(StackCrossAlignment.Start).Main(StackMainAlignment.Start);
        Entity picker = panel;
        var parts = new EasePickerParts();

        UiNode title = panel.Row().Size(Inner, TitleHeight).Main(StackMainAlignment.Start);
        title.Text("EASING", UiType.Label, UiTheme.Accent).Size(Inner - TitleHeight, TitleHeight);
        parts.Close = title.Button("×", UiType.Caption).Size(TitleHeight, TitleHeight).Tooltip("Close the easing picker");

        UiNode body = panel.Row().Size(Inner, GridHeight).Gap(Spacing)
            .Cross(StackCrossAlignment.Start).Main(StackMainAlignment.Start);

        UiNode preview = body.Column().Size(PreviewSize.X, GridHeight).Gap(6)
            .Cross(StackCrossAlignment.Start).Main(StackMainAlignment.Start);
        parts.PreviewBox = preview.Rect(Palette.SurfaceInset, size: PreviewSize);
        parts.Name = preview.Text("", UiType.Body, UiTheme.Text).Size(PreviewSize.X, 22);
        parts.Description = preview.Text("", UiType.Caption, UiTheme.TextDim).Size(PreviewSize.X, 18);
        parts.Hint = preview.Text("", UiType.Caption, UiTheme.TextDim).Size(PreviewSize.X, 18);

        BuildGrid(body, picker, host);

        // The preview's drawing floats over its box, placed by the system, so
        // the box's own layout never moves it.
        UiNode floating = panel.LayerNode;
        parts.Guides = Float(floating.Shape(Line(Vector2.Right), Clear, Guide, 1f, closed: false));
        parts.Curve = Float(floating.Shape(Line(Vector2.Right), Clear, Stroke, 2f, closed: false));
        parts.Rail = Float(floating.Shape(Line(Vector2.Down), Clear, Guide, 2f, closed: false));
        parts.Dot = Float(floating.Shape(Circle(DotRadius), DotColor));
        parts.RailDot = Float(floating.Shape(Circle(DotRadius), DotColor));

        panel.With(new EasePicker { Host = host, Parts = parts });
        return picker;
    }

    /// <summary>Column labels, the basic row, then one row per family.</summary>
    private static void BuildGrid(UiNode body, Entity picker, IEasePickerHost host)
    {
        UiNode grid = body.Column().Size(GridWidth, GridHeight).Gap(Gap)
            .Cross(StackCrossAlignment.Start).Main(StackMainAlignment.Start);

        UiNode header = Row(grid, ColumnLabelHeight);
        header.Text("", UiType.Caption).Size(RowLabelWidth, ColumnLabelHeight);
        foreach (CurveDirection direction in EaseNames.Directions)
        {
            header.Text(EaseNames.Label(direction), UiType.Caption, UiTheme.TextDim, TextAlign.Center)
                .Size(CellSize.X, ColumnLabelHeight);
        }

        UiNode basics = Row(grid, CellSize.Y);
        RowLabel(basics, "BASIC");
        foreach (EasePreset preset in EaseNames.Basics)
            CellButton(basics, preset, picker, host);

        foreach (CurveFamily family in EaseNames.Families)
        {
            UiNode row = Row(grid, CellSize.Y);
            RowLabel(row, EaseNames.Label(family));
            foreach (CurveDirection direction in EaseNames.Directions)
                CellButton(row, EasePresets.Preset(family, direction), picker, host);
        }
    }

    private static UiNode Row(UiNode grid, float height) =>
        grid.Row().Size(GridWidth, height).Gap(Gap).Main(StackMainAlignment.Start);

    private static void RowLabel(UiNode row, string label) =>
        row.Text(label, UiType.Caption, UiTheme.TextDim).Size(RowLabelWidth, CellSize.Y);

    /// <summary>A thumbnail of the curve that picks it when pressed.</summary>
    private static void CellButton(UiNode row, EasePreset preset, Entity picker, IEasePickerHost host)
    {
        UiNode cell = row.Row().Size(CellSize).Fill(Cell).Input()
            .With(ButtonVisual.Create(Cell))
            .With(new EasePickerCell(preset, picker))
            .Tooltip($"{EaseNames.Label(preset)}: {EaseNames.Describe(preset)}{host.TooltipNote(preset)}");

        Entity thumb = cell.Shape(CurvePoints(preset, ThumbSize, 24), Clear, Stroke, 1.5f, closed: false);
        thumb.GetComponent<Rectangle>().Bounds = new Rect2(-ThumbSize / 2, ThumbSize);
    }

    /// <summary>
    /// The curve drawn into a box centred on the origin, left to right and
    /// bottom to top. The box takes the curve's full range, overshoot
    /// included; <paramref name="low"/> and <paramref name="high"/> say where
    /// that range ends, so guides can be drawn at 0 and 1.
    /// </summary>
    public static Vector2[] CurvePoints(EasePreset preset, Vector2 size, int steps) =>
        CurvePoints(preset, size, steps, out _, out _);

    public static Vector2[] CurvePoints(EasePreset preset, Vector2 size, int steps, out float low, out float high)
    {
        low = 0f;
        high = 1f;
        if (preset == EasePreset.Step)
        {
            Vector2 half = size / 2;
            return new[] { new Vector2(-half.X, half.Y), new Vector2(half.X, half.Y), new Vector2(half.X, -half.Y) };
        }

        var values = new float[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            values[i] = EasePresets.Progress(preset, i / (float)steps);
            low = Math.Min(low, values[i]);
            high = Math.Max(high, values[i]);
        }

        var points = new Vector2[steps + 1];
        for (int i = 0; i <= steps; i++)
            points[i] = Point(i / (float)steps, values[i], size, low, high);
        return points;
    }

    /// <summary>Where a share of the span and a progress value land in a box centred on the origin.</summary>
    public static Vector2 Point(float t, float value, Vector2 size, float low, float high)
    {
        float rise = (value - low) / Math.Max(0.0001f, high - low);
        return new Vector2((t - 0.5f) * size.X, (0.5f - rise) * size.Y);
    }

    private static Entity Float(UiNode shape) => shape.IgnoreLayout().Overlay().Hidden();

    private static Vector2[] Line(Vector2 to) => new[] { Vector2.Zero, to };

    private static Vector2[] Circle(float radius)
    {
        const int sides = 16;
        var points = new Vector2[sides];
        for (int i = 0; i < sides; i++)
        {
            float angle = Mathf.Tau * i / sides;
            points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        return points;
    }
}

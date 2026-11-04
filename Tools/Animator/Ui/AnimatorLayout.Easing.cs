using System;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Theme;

namespace Xanadu.Tools.Animator;

/// <summary>
/// The EASING tool window, opened from the timeline header: a live preview of
/// a curve at the left, and every curve at the right as a thumbnail, one row
/// per family and one column per direction. Clicking a thumbnail sets it on
/// the selected keys; the window stays open so keys can be picked in turn.
/// </summary>
public static partial class AnimatorLayout
{
    /// <summary>The cells' fill; the selected keys' curve is lit with the selection colour.</summary>
    public static readonly Color EasingCell = Palette.SurfaceAlt;

    /// <summary>The curves, in the thumbnails and the preview.</summary>
    public static readonly Color EasingStroke = Palette.Text;

    /// <summary>The 0 and 1 guide lines behind the preview curve.</summary>
    public static readonly Color EasingGuide = Palette.WithAlpha(Palette.TextDim, 0.35f);

    /// <summary>The dots that run the preview curve.</summary>
    public static readonly Color EasingDot = Palette.Accent;

    public const float EasingButtonWidth = 150f;

    /// <summary>The live preview's box, inside which the system draws.</summary>
    public static readonly Vector2 EasingPreviewSize = new(230f, 170f);

    /// <summary>How far in from the box's edges the curve keeps.</summary>
    public const float EasingPreviewInset = 18f;

    /// <summary>Where the value rail sits, in from the box's right edge.</summary>
    public const float EasingRailInset = 10f;

    public const float EasingDotRadius = 5f;

    private const float EasingPadding = 12f;
    private const float EasingSpacing = 12f;
    private const float EasingTitleHeight = 24f;
    private const float EasingRowLabelWidth = 62f;
    private const float EasingColumnLabelHeight = 18f;
    private const float EasingGap = 4f;
    private static readonly Vector2 EasingCellSize = new(62f, 36f);
    private static readonly Vector2 EasingThumbSize = new(48f, 22f);

    private static float EasingGridWidth =>
        EasingRowLabelWidth + (EasingCellSize.X + EasingGap) * 3;

    private static float EasingGridHeight =>
        EasingColumnLabelHeight + (EasingCellSize.Y + EasingGap) * (AnimatorEasingNames.Families.Length + 1);

    public static Vector2 EasingWindowSize => new(
        EasingPadding * 2 + EasingPreviewSize.X + EasingSpacing + EasingGridWidth,
        EasingPadding * 2 + EasingTitleHeight + EasingSpacing + EasingGridHeight);

    /// <summary>The header button that opens the window and names the selected keys' curve.</summary>
    private static Entity EasingButton(UiBuilder ui) =>
        HeaderButton(ui, "EASING", ToolActionKind.ToggleEasingWindow, EasingButtonWidth);

    private static void BuildEasingWindow(UiBuilder menu, ref Refs refs)
    {
        Vector2 size = EasingWindowSize;
        float inner = size.X - EasingPadding * 2;
        menu.Container(Vector2.Zero, size, StackDirection.Vertical, spacing: EasingSpacing,
            color: UiTheme.Panel, padding: EasingPadding, takesInput: true,
            cross: StackCrossAlignment.Start, main: StackMainAlignment.Start);
        UiLayout.SetHidden(menu.Current, true);
        refs.Ui.EasingWindow = menu.Current;
        refs.Ui.EasingWindow.AddTag<UiOverlayItem>();

        menu.Container(Vector2.Zero, new Vector2(inner, EasingTitleHeight), color: Clear,
            main: StackMainAlignment.Start);
        menu.Text("EASING", new Vector2(inner - EasingTitleHeight, EasingTitleHeight), UiTheme.FontSize,
            UiTheme.Accent, TextAlign.Left);
        Action(menu, "×", ToolActionKind.ToggleEasingWindow, new Vector2(EasingTitleHeight, EasingTitleHeight));
        menu.End();

        menu.Container(Vector2.Zero, new Vector2(inner, EasingGridHeight), spacing: EasingSpacing,
            color: Clear, cross: StackCrossAlignment.Start, main: StackMainAlignment.Start);
        BuildEasingPreview(menu, ref refs);
        BuildEasingGrid(menu);
        menu.End();
        menu.End();

        // The preview's drawing floats over its box, placed by the system,
        // so the box's own layout never moves it.
        refs.Ui.EasingGuides = menu.ShapeEntity(new[] { Vector2.Zero, Vector2.Right }, Clear,
            EasingGuide, 1f, closed: false);
        refs.Ui.EasingCurve = menu.ShapeEntity(new[] { Vector2.Zero, Vector2.Right }, Clear,
            EasingStroke, 2f, closed: false);
        refs.Ui.EasingRail = menu.ShapeEntity(new[] { Vector2.Zero, Vector2.Down }, Clear,
            EasingGuide, 2f, closed: false);
        refs.Ui.EasingDot = menu.ShapeEntity(Circle(EasingDotRadius), EasingDot);
        refs.Ui.EasingRailDot = menu.ShapeEntity(Circle(EasingDotRadius), EasingDot);
        foreach (Entity part in new[]
                 {
                     refs.Ui.EasingGuides, refs.Ui.EasingCurve, refs.Ui.EasingRail,
                     refs.Ui.EasingDot, refs.Ui.EasingRailDot,
                 })
        {
            part.AddTag<UiOverlayItem>();
            UiLayout.SetHidden(part, true);
        }
    }

    private static void BuildEasingPreview(UiBuilder menu, ref Refs refs)
    {
        menu.Container(Vector2.Zero, new Vector2(EasingPreviewSize.X, EasingGridHeight), StackDirection.Vertical,
            spacing: 6, color: Clear, cross: StackCrossAlignment.Start, main: StackMainAlignment.Start);
        refs.Ui.EasingPreviewBox = menu.RectEntity(Vector2.Zero, EasingPreviewSize, Palette.SurfaceInset);
        refs.Ui.EasingName = menu.Text("", new Vector2(EasingPreviewSize.X, 22), UiTheme.FontSize,
            UiTheme.Text, TextAlign.Left);
        refs.Ui.EasingDescription = menu.Text("", new Vector2(EasingPreviewSize.X, 18), UiTheme.SmallFontSize,
            UiTheme.TextDim, TextAlign.Left);
        refs.Ui.EasingHint = menu.Text("", new Vector2(EasingPreviewSize.X, 18), UiTheme.SmallFontSize,
            UiTheme.TextDim, TextAlign.Left);
        menu.End();
    }

    /// <summary>Column labels, the basic row, then one row per family.</summary>
    private static void BuildEasingGrid(UiBuilder menu)
    {
        menu.Container(Vector2.Zero, new Vector2(EasingGridWidth, EasingGridHeight), StackDirection.Vertical,
            spacing: EasingGap, color: Clear, cross: StackCrossAlignment.Start, main: StackMainAlignment.Start);

        EasingRow(menu, EasingColumnLabelHeight);
        menu.Text("", new Vector2(EasingRowLabelWidth, EasingColumnLabelHeight), UiTheme.SmallFontSize);
        foreach (XafCurveDirection direction in AnimatorEasingNames.Directions)
        {
            menu.Text(AnimatorEasingNames.Label(direction), new Vector2(EasingCellSize.X, EasingColumnLabelHeight),
                UiTheme.SmallFontSize, UiTheme.TextDim);
        }
        menu.End();

        EasingRow(menu, EasingCellSize.Y);
        EasingRowLabel(menu, "BASIC");
        foreach (XafEasePreset preset in AnimatorEasingNames.Basics)
            EasingCellButton(menu, preset);
        menu.End();

        foreach (XafCurveFamily family in AnimatorEasingNames.Families)
        {
            EasingRow(menu, EasingCellSize.Y);
            EasingRowLabel(menu, AnimatorEasingNames.Label(family));
            foreach (XafCurveDirection direction in AnimatorEasingNames.Directions)
                EasingCellButton(menu, XafEasing.Preset(family, direction));
            menu.End();
        }

        menu.End();
    }

    private static void EasingRow(UiBuilder menu, float height) =>
        menu.Container(Vector2.Zero, new Vector2(EasingGridWidth, height), spacing: EasingGap,
            color: Clear, main: StackMainAlignment.Start);

    private static void EasingRowLabel(UiBuilder menu, string label) =>
        menu.Text(label, new Vector2(EasingRowLabelWidth, EasingCellSize.Y), UiTheme.SmallFontSize,
            UiTheme.TextDim, TextAlign.Left);

    /// <summary>A thumbnail of the curve that sets it on the selected keys when clicked.</summary>
    private static void EasingCellButton(UiBuilder menu, XafEasePreset preset)
    {
        menu.Container(Vector2.Zero, EasingCellSize, color: EasingCell, takesInput: true);
        Entity cell = menu.Current;
        cell.AddComponent(ButtonVisual.Create(EasingCell));
        cell.AddComponent(new ToolAction(ToolActionKind.Interpolate, preset.ToString()));
        cell.AddTag<AnimatorEasingCell>();
        AnimatorTooltips.Attach(cell, ToolActionKind.Interpolate, preset.ToString());

        Entity thumb = menu.ShapeEntity(EasingCurvePoints(preset, EasingThumbSize, 24), Clear,
            EasingStroke, 1.5f, closed: false);
        thumb.GetComponent<Rectangle>().Bounds = new Rect2(-EasingThumbSize / 2, EasingThumbSize);
        menu.End();
    }

    /// <summary>
    /// The curve drawn into a box centred on the origin, left to right and
    /// bottom to top. The box takes the curve's full range, overshoot
    /// included; <paramref name="low"/> and <paramref name="high"/> say where
    /// that range ends, so guides can be drawn at 0 and 1.
    /// </summary>
    public static Vector2[] EasingCurvePoints(XafEasePreset preset, Vector2 size, int steps) =>
        EasingCurvePoints(preset, size, steps, out _, out _);

    public static Vector2[] EasingCurvePoints(XafEasePreset preset, Vector2 size, int steps,
        out float low, out float high)
    {
        if (preset == XafEasePreset.Step)
        {
            low = 0f;
            high = 1f;
            Vector2 half = size / 2;
            return new[]
            {
                new Vector2(-half.X, half.Y),
                new Vector2(half.X, half.Y),
                new Vector2(half.X, -half.Y),
            };
        }

        var values = new float[steps + 1];
        low = 0f;
        high = 1f;
        for (int i = 0; i <= steps; i++)
        {
            values[i] = XafEasing.Progress(preset, i / (float)steps);
            low = Math.Min(low, values[i]);
            high = Math.Max(high, values[i]);
        }

        var points = new Vector2[steps + 1];
        for (int i = 0; i <= steps; i++)
            points[i] = EasingPoint(i / (float)steps, values[i], size, low, high);
        return points;
    }

    /// <summary>Where a share of the span and a progress value land in a box centred on the origin.</summary>
    public static Vector2 EasingPoint(float t, float value, Vector2 size, float low, float high)
    {
        float rise = (value - low) / Math.Max(0.0001f, high - low);
        return new Vector2((t - 0.5f) * size.X, (0.5f - rise) * size.Y);
    }

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

using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Systems.Editor;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Scenes;

/// <summary>
/// The selection's ALIGN row: six buttons that line the selected items up on
/// their left, centre, right, top, middle or bottom. Each icon is the line
/// the items meet on and two bars of different lengths meeting it.
/// </summary>
public static class EditorSceneAlign
{
    private const float Line = 2;
    private const float Bar = 5;
    private const float Long = 16;
    private const float Short = 10;

    internal static void Build(UiNode panel, Entity editor, float inner)
    {
        UiNode section = panel.Column()
            .Width(inner)
            .AutoHeight()
            .Gap(UiTheme.ContainerSpacing)
            .Main(StackMainAlignment.Start)
            .Hidden();

        EditorSceneUi.Separator(section, inner);
        section.Text("ALIGN", UiTheme.SmallFontSize, SceneUi.DimTextColor, TextAlign.Left)
            .Size(inner, UiMetrics.Control.Xs);

        const int count = 6;
        float gap = UiTheme.ContainerSpacing;
        float side = Mathf.Floor((inner - gap * (count - 1)) / count);
        UiNode row = section.Row().Size(inner, side).Gap(gap).Main(StackMainAlignment.Start);
        Button(row, side, AlignEdge.Left, "Line the selection up on its left edges");
        Button(row, side, AlignEdge.CenterX, "Line the selection up on its centres, left to right");
        Button(row, side, AlignEdge.Right, "Line the selection up on its right edges");
        Button(row, side, AlignEdge.Top, "Line the selection up on its top edges");
        Button(row, side, AlignEdge.CenterY, "Line the selection up on its middles, top to bottom");
        Button(row, side, AlignEdge.Bottom, "Line the selection up on its bottom edges");

        editor.AddComponent(new EditorAlignPanel(section.Entity));
    }

    private static void Button(UiNode row, float side, AlignEdge edge, string tooltip)
    {
        // Lining up left to right puts the line beside the bars; top to bottom, above or below them.
        bool across = edge is AlignEdge.Left or AlignEdge.CenterX or AlignEdge.Right;
        UiNode button = (across ? row.Row() : row.Column())
            .Size(side, side)
            .Fill(SceneUi.MutedButtonColor)
            .Input()
            .Gap(Line)
            .Main(StackMainAlignment.Center)
            .Cross(StackCrossAlignment.Center)
            .With(ButtonVisual.Create(SceneUi.MutedButtonColor))
            .With(EditorButton.Of(EditorButtonKind.Align, (int)edge))
            .Tooltip(tooltip);

        StackCrossAlignment meet = edge switch
        {
            AlignEdge.Left or AlignEdge.Top => StackCrossAlignment.Start,
            AlignEdge.Right or AlignEdge.Bottom => StackCrossAlignment.End,
            _ => StackCrossAlignment.Center,
        };

        float depth = Bar * 2 + Line;
        if (meet == StackCrossAlignment.Start)
            LineMark(button, across, depth);

        // The bars run across the line they meet.
        UiNode bars = (across ? button.Column() : button.Row())
            .Size(across ? Long : depth, across ? depth : Long)
            .Gap(Line)
            .Main(StackMainAlignment.Center)
            .Cross(meet);
        bars.Rect(UiTheme.Text, size: across ? new Vector2(Long, Bar) : new Vector2(Bar, Long));
        bars.Rect(UiTheme.Text, size: across ? new Vector2(Short, Bar) : new Vector2(Bar, Short));

        if (meet == StackCrossAlignment.End)
            LineMark(button, across, depth);
    }

    /// <summary>The line the items meet on, a little longer than the bars are deep.</summary>
    private static void LineMark(UiNode button, bool across, float depth) =>
        button.Rect(UiTheme.Accent, size: across ? new Vector2(Line, depth + 6) : new Vector2(depth + 6, Line));
}

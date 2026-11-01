using Godot;
using Xanadu.Theme;

namespace Xanadu.Ecs;

public readonly partial struct UiNode
{
    /// <summary>A labelled swatch and hex field in one row; returns the field, which carries <see cref="UiColorField"/>.</summary>
    public UiNode ColorField(string label, float width, string tooltip)
    {
        const float height = 28;
        const float gap = 6;
        const float labelWidth = 68;
        UiNode row = Row().Size(width, height).Gap(gap).Main(StackMainAlignment.Start);
        row.Text(label, UiTheme.SmallFontSize, UiTheme.TextDim, TextAlign.Left).Size(labelWidth, height);
        UiNode swatch = row.Rect(Colors.White, size: new Vector2(height, height));
        return row.Rect(Palette.SurfaceInset, "", new Vector2(width - labelWidth - height - gap * 2, height))
            .Input()
            .With(new TextStyle(13, UiTheme.Text))
            .Align(TextAlign.Left)
            .With(new TextField(""))
            .With(new UiColorField(swatch.Entity))
            .Tooltip(tooltip);
    }
}

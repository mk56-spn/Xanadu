using Godot;
using Xanadu.Theme;

namespace Xanadu.Systems.Debug;

/// <summary>Look shared by the interactive debug panels (the F8 shader tuner).</summary>
public static class DebugPanelStyle
{
    public static readonly Color Background = Palette.WithAlpha(Palette.Background, 0.94f);
    public static readonly Color Accent = new("#ffc035");
    public static readonly Color Text = Palette.Text;
    public const int TitleSize = 18;
    public const int RowSize = 16;
}

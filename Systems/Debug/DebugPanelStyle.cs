using Godot;

namespace Xanadu.Systems.Debug;

/// <summary>Look shared by the interactive debug panels (F6 gravity, F8 shaders).</summary>
public static class DebugPanelStyle
{
    public static readonly Color Background = new(0.04f, 0.06f, 0.1f, 0.94f);
    public static readonly Color Accent = new("#ffc035");
    public static readonly Color Text = new("#e2e8f0");
    public const int TitleSize = 18;
    public const int RowSize = 16;
}

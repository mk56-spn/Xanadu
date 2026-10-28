using Godot;
using Xanadu.Theme;

namespace Xanadu.Ecs;

/// <summary>Shared defaults for the generic ECS UI.</summary>
public static class UiTheme
{
    public static readonly Color Background = Palette.Background;
    public static readonly Color Panel = Palette.Surface;
    public static readonly Color PanelAlt = Palette.SurfaceAlt;
    public static readonly Color Button = Palette.Primary;
    public static readonly Color ButtonMuted = Palette.Muted;
    public static readonly Color DropdownOption = Palette.SurfaceAlt;
    public static readonly Color Text = Palette.Text;
    public static readonly Color TextDim = Palette.TextDim;
    public static readonly Color Accent = Palette.Accent;
    public static readonly Color Selected = Palette.Selected;
    public static readonly Color Focus = Palette.TextBright;
    public static readonly Color Disabled = Palette.SurfaceDisabled;
    public static readonly Color Tooltip = Palette.Tooltip;

    public const int FontSize = 14;
    public const int SmallFontSize = 12;
    public const int HeadingFontSize = 24;
    public const float ButtonHeight = 32f;
    public const float DropdownOptionHeight = 28f;
    public const float ContainerPadding = 12f;
    public const float ContainerSpacing = 8f;
    public const float DropdownOptionSpacing = 2f;
    public const float TreeRowHeight = 26f;
    public const float TreeIndent = 16f;
    public const int FocusLayer = 995;
    public const int TooltipLayer = 996;
}

using Godot;

namespace Writer.Ecs;

/// <summary>Shared defaults for the generic ECS UI.</summary>
public static class UiTheme
{
    public static readonly Color Background = new("#0b1020");
    public static readonly Color Panel = new("#111827");
    public static readonly Color PanelAlt = new("#1e293b");
    public static readonly Color Button = new("#2563eb");
    public static readonly Color ButtonMuted = new("#334155");
    public static readonly Color DropdownOption = new("#1e293b");
    public static readonly Color Text = new("#e2e8f0");
    public static readonly Color TextDim = new("#94a3b8");
    public static readonly Color Accent = new("#facc15");

    public const int FontSize = 14;
    public const int SmallFontSize = 12;
    public const int HeadingFontSize = 24;
    public const float ButtonHeight = 32f;
    public const float DropdownOptionHeight = 28f;
    public const float ContainerPadding = 12f;
    public const float ContainerSpacing = 8f;
    public const float DropdownOptionSpacing = 2f;
}

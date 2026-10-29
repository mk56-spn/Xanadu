using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Theme;

namespace Xanadu.Scenes;

/// <summary>
/// Shared layer ids and palette aliases for game scene UI. Construction lives
/// on <see cref="UiNode"/>; this type only names things.
/// </summary>
public static class SceneUi
{
    public const int MenuLayer = 100;
    public const int PlayHudLayer = 900;
    public const int ConfirmationLayer = 970;
    public const int PauseLayer = 980;

    public static readonly Color BackgroundColor = Palette.Background;
    public static readonly Color PanelColor = Palette.Surface;
    public static readonly Color ButtonColor = Palette.Primary;
    public static readonly Color MutedButtonColor = Palette.Muted;
    public static readonly Color AccentColor = Palette.Accent;
    public static readonly Color DangerColor = Palette.Danger;
    public static readonly Color TextColor = Palette.Text;
    public static readonly Color DimTextColor = Palette.TextDim;

    public static void SetText(Entity entity, string text) => UiText.Set(entity, text);
}

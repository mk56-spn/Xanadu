using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Theme;

namespace Xanadu.Scenes;

/// <summary>
/// Shared palette aliases for game scene UI. Construction lives
/// on <see cref="UiNode"/>; this type only names things.
/// </summary>
public static class SceneUi
{
    public static readonly Color BackgroundColor = Palette.Background;
    public static readonly Color PanelColor = Palette.Surface;
    public static readonly Color ButtonColor = Palette.Primary;
    public static readonly Color MutedButtonColor = Palette.Muted;

    /// <summary>The editor's buttons at rest: muted blue, leaving the bright blue for what is toggled on.</summary>
    public static readonly Color EditorButtonColor = Palette.PrimaryMuted;
    public static readonly Color AccentColor = Palette.Accent;
    public static readonly Color DangerColor = Palette.Danger;
    public static readonly Color TextColor = Palette.Text;
    public static readonly Color DimTextColor = Palette.TextDim;

    /// <summary>The menus' one accent, from song select; new UI should ask its theme (<see cref="UiThemes.Menu"/>) instead.</summary>
    public static readonly Color GoldColor = UiThemes.Menu.Accent;
    public static readonly Color GoldLightColor = Palette.GoldLight;

    /// <summary>Space a backing leaves around what it sits behind.</summary>
    public const float BackingPad = 20f;

    public static void SetText(Entity entity, string text) => UiText.Set(entity, text);
}

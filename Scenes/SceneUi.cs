using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Theme;

namespace Xanadu.Scenes;

/// <summary>
/// Shared layer ids and palette aliases for game scene UI. Construction lives on
/// <see cref="UiNode"/> (and the scene-level helpers in
/// <see cref="UiNodeActions"/> / <see cref="HudUi"/>); only <see cref="Text"/>
/// and <see cref="Layer"/> remain here for the editor partials that have not
/// moved yet.
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

    public static UiBuilder Layer(
        EntityStore entities,
        Entity root,
        int zIndex,
        IVisualRidFactory? visuals = null)
    {
        UiBuilder builder = UiBuilder.Begin(entities, visuals).Layer(zIndex);
        root.AddChild(builder.Current);
        return builder;
    }

    /// <summary>
    /// A fixed-box label backed by the window colour. Migrated code uses
    /// <see cref="UiNode.Text"/> instead; this stays for the editor partials.
    /// </summary>
    public static Entity Text(
        UiBuilder builder,
        Vector2 center,
        Vector2 size,
        string text,
        int fontSize,
        Color color,
        Color? background = null)
    {
        Entity entity = builder.RectEntity(
            center,
            size,
            background ?? BackgroundColor,
            text);

        entity.AddComponent(new TextStyle(fontSize, color));
        return entity;
    }

    public static void SetText(Entity entity, string text) => UiText.Set(entity, text);
}

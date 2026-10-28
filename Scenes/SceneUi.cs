using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Theme;

namespace Xanadu.Scenes;

/// <summary>Shared ECS entities and styling for game scene UI.</summary>
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

    public static Entity Background(UiBuilder builder, Vector2 screen)
    {
        Entity background =
            builder.RectEntity(screen / 2, screen, BackgroundColor);
        background.AddComponent(new UiBackground());
        return background;
    }

    public static Entity Panel(
        UiBuilder builder,
        Vector2 center,
        Vector2 size,
        Color? color = null) =>
        builder.RectEntity(center, size, color ?? PanelColor);

    /// <summary>A transparent text label drawn above its parent panel.</summary>
    public static Entity Label(
        UiBuilder builder,
        Vector2 center,
        Vector2 size,
        string text,
        int fontSize,
        Color color,
        Color? background = null) =>
        Text(
            builder,
            center,
            size,
            text,
            fontSize,
            color,
            background ?? new Color(0, 0, 0, 0));

    /// <summary>A styled button entity without an attached action component.</summary>
    public static Entity ButtonEntity(
        UiBuilder builder,
        Vector2 center,
        Vector2 size,
        string label,
        int fontSize = 18,
        Color? color = null)
    {
        Color normal = color ?? ButtonColor;
        Entity button = builder.RectEntity(
            center,
            size,
            normal,
            label,
            takesInput: true);
        button.AddComponent(new TextStyle(fontSize, TextColor));
        button.AddComponent(ButtonVisual.Create(normal));
        return button;
    }

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

    public static Entity Button(
        UiBuilder builder,
        Vector2 center,
        Vector2 size,
        string label,
        SceneKind target,
        int fontSize = 18,
        Color? color = null)
    {
        Color normal = color ?? ButtonColor;

        Entity button = builder.RectEntity(
            center,
            size,
            normal,
            label,
            takesInput: true);

        button.AddComponent(new TextStyle(fontSize, TextColor));
        button.AddComponent(ButtonVisual.Create(normal));
        button.AddComponent(new SceneAction(target));
        return button;
    }

    public static Entity CarouselButton(
        UiBuilder builder,
        Vector2 center,
        Vector2 size,
        string label,
        int direction)
    {
        Entity button = builder.RectEntity(
            center,
            size,
            MutedButtonColor,
            label,
            takesInput: true);

        button.AddComponent(new TextStyle(24, TextColor));
        button.AddComponent(ButtonVisual.Create(MutedButtonColor));
        button.AddComponent(new SceneCommand(
            direction < 0 ? SceneActionId.Previous : SceneActionId.Next));
        return button;
    }

    public static void SetText(Entity entity, string text) => UiText.Set(entity, text);

/// <summary>Creates a clipped vertical scrolling viewport and returns its content.</summary>
public static Entity ScrollView(
    UiBuilder ui,
    Vector2 center,
    Vector2 size,
    float contentHeight,
    out Entity content)
{
    ui.Container(
        center,
        size,
        StackDirection.Vertical,
        spacing: 0,
        clip: true,
        color: new Color(0, 0, 0, 0),
        takesInput: true);
    Entity viewport = ui.Current;

    float baseContentY = -size.Y / 2 + contentHeight / 2;
    ui.Container(
        new Vector2(0, baseContentY),
        new Vector2(size.X, contentHeight),
        StackDirection.Vertical,
        spacing: 4,
        color: new Color(0, 0, 0, 0));
    content = ui.Current;
    viewport.AddComponent(new ScrollView(content));
    return viewport;
}

}

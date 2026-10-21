using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Writer.Ui;

namespace Writer.Scenes;

/// <summary>Shared ECS entities and styling for game scene UI.</summary>
public static class SceneUi
{
    public const int MenuLayer = 100;
    public const int PlayHudLayer = 900;
    public const int SceneLabelLayer = 950;
    public const int ConfirmationLayer = 970;
    public const int PauseLayer = 980;

    public static readonly Color BackgroundColor = new("#0b1020");
    public static readonly Color PanelColor = new("#111827");
    public static readonly Color ButtonColor = new("#2563eb");
    public static readonly Color MutedButtonColor = new("#334155");
    public static readonly Color AccentColor = new("#facc15");
    public static readonly Color TextColor = new("#e2e8f0");
    public static readonly Color DimTextColor = new("#94a3b8");

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

    public static Entity Background(UiBuilder builder, Vector2 screen) =>
        builder.RectEntity(screen / 2, screen, BackgroundColor);

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
        button.AddComponent(new CarouselStep(direction));
        return button;
    }

    public static void SetText(Entity entity, string text)
    {
        if (entity.IsNull || !entity.HasComponent<Text>())
            return;

        ref Text component = ref entity.GetComponent<Text>();
        if (component.Value == text)
            return;

        component.Value = text;
        entity.AddTag<Dirty>();
    }
}

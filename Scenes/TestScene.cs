using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Position = Writer.Ecs.Position;

namespace Writer.Scenes;

/// <summary>Creates throwaway layers and rectangles for exercising the ECS systems.</summary>
public static class TestScene
{
    public static void Build(EntityStore entities)
    {
        CreateLayer(entities, 0);
        CreateLayer(entities, 5);
        CreateLayer(entities, 10);

        AddRect(entities, 0, new Vector2(140, 80), new Vector2(200, 80), Colors.CornflowerBlue, "Layer 0", true);
        AddRect(entities, 0, new Vector2(340, 80), new Vector2(120, 80), Colors.CornflowerBlue, "Layer 0", false);

        AddRect(entities, 5, new Vector2(120, 190), new Vector2(160, 60), Colors.Orange, "Layer 5", true);
        AddRect(entities, 5, new Vector2(340, 210), new Vector2(200, 100), Colors.Orange, "Layer 5", false);

        AddRect(entities, 10, new Vector2(240, 360), new Vector2(320, 120), Colors.MediumSeaGreen, "Layer 10", true);
        AddRect(entities, 10, new Vector2(490, 350), new Vector2(100, 100), Colors.MediumSeaGreen, "Layer 10", false);

        var container = AddContainer(
            entities,
            layerZIndex: 10,
            center: new Vector2(850, 200),
            size: new Vector2(360, 180),
            StackDirection.Horizontal,
            spacing: 12);

        AddChildRect(entities, container, new Vector2(90, 70), Colors.SkyBlue, "A", false);
        AddChildRect(entities, container, new Vector2(90, 70), Colors.Gold, "B", true);
        AddChildRect(entities, container, new Vector2(90, 70), Colors.LightGreen, "C", false);
    }

    private static void CreateLayer(EntityStore entities, int zIndex) =>
        entities.CreateEntity(new VisualRid(), new Layer(zIndex), Tags.Get<Uninitialised>());

    private static Entity AddContainer(
        EntityStore entities,
        int layerZIndex,
        Vector2 center,
        Vector2 size,
        StackDirection direction,
        float spacing) =>
        entities.CreateEntity(
            new VisualRid(),
            new Rectangle(new Rect2(-size / 2, size)),
            new Drawable(layerZIndex),
            new Position(center),
            new FillColor(new Color("#1e293b")),
            new UiContainer(direction, spacing, clip: true),
            Tags.Get<Uninitialised, Dirty, LayoutDirty>());

    private static void AddChildRect(
        EntityStore entities,
        Entity container,
        Vector2 size,
        Color color,
        string text,
        bool takesInput)
    {
        Tags tags = takesInput
            ? Tags.Get<Uninitialised, Dirty, TakesInput>()
            : Tags.Get<Uninitialised, Dirty>();

        var child = entities.CreateEntity(
            new VisualRid(),
            new Rectangle(new Rect2(-size / 2, size)),
            new Drawable(10),
            new Position(Vector2.Zero),
            new FillColor(color),
            new Text(text),
            tags);

        container.AddChild(child);
    }

    private static void AddRect(
        EntityStore entities,
        int layerZIndex,
        Vector2 center,
        Vector2 size,
        Color color,
        string text,
        bool takesInput)
    {
        Tags tags = takesInput
            ? Tags.Get<Uninitialised, Dirty, TakesInput>()
            : Tags.Get<Uninitialised, Dirty>();

        entities.CreateEntity(
            new VisualRid(),
            new Rectangle(new Rect2(-size / 2, size)),
            new Drawable(layerZIndex),
            new Position(center),
            new FillColor(color),
            new Text(text),
            tags);
    }
}

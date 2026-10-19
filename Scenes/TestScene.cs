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

        AddRect(entities, 0, new Vector2(140, 80), new Vector2(200, 80), Colors.CornflowerBlue, "Layer 0");
        AddRect(entities, 0, new Vector2(340, 80), new Vector2(120, 80), Colors.CornflowerBlue, "Layer 0");

        AddRect(entities, 5, new Vector2(120, 190), new Vector2(160, 60), Colors.Orange, "Layer 5");
        AddRect(entities, 5, new Vector2(340, 210), new Vector2(200, 100), Colors.Orange, "Layer 5");

        AddRect(entities, 10, new Vector2(240, 360), new Vector2(320, 120), Colors.MediumSeaGreen, "Layer 10");
        AddRect(entities, 10, new Vector2(490, 350), new Vector2(100, 100), Colors.MediumSeaGreen, "Layer 10");
    }

    private static void CreateLayer(EntityStore entities, int zIndex) =>
        entities.CreateEntity(new VisualRid(), new Layer(zIndex), Tags.Get<Uninitialised>());

    private static void AddRect(
        EntityStore entities,
        int layerZIndex,
        Vector2 center,
        Vector2 size,
        Color color,
        string text) =>
        entities.CreateEntity(
            new VisualRid(),
            new Rectangle(new Rect2(-size / 2, size)),
            new Drawable(layerZIndex),
            new Position(center),
            new FillColor(color),
            new Text(text),
            Tags.Get<Uninitialised, Dirty>());
}

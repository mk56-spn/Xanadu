using Friflo.Engine.ECS;
using Godot;

namespace Writer;

/// <summary>Creates throwaway layers and rectangles for exercising the ECS systems.</summary>
public static class TestScene
{
    public static void Build(EntityStore entities)
    {
        CreateLayer(entities, 0);
        CreateLayer(entities, 5);
        CreateLayer(entities, 10);

        AddRect(entities, 0, new Rect2(40, 40, 200, 80));
        AddRect(entities, 0, new Rect2(280, 40, 120, 80));

        AddRect(entities, 5, new Rect2(40, 160, 160, 60));
        AddRect(entities, 5, new Rect2(240, 160, 200, 100));

        AddRect(entities, 10, new Rect2(80, 300, 320, 120));
        AddRect(entities, 10, new Rect2(440, 300, 100, 100));
    }

    private static void CreateLayer(EntityStore entities, int zIndex) =>
        entities.CreateEntity(new VisualRid(), new Layer(zIndex), Tags.Get<Uninitialised>());

    private static void AddRect(EntityStore entities, int layerZIndex, Rect2 bounds) =>
        entities.CreateEntity(
            new VisualRid(),
            new Rectangle(bounds),
            new Drawable(layerZIndex),
            Tags.Get<Uninitialised>());
}

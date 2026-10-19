using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Position = Writer.Ecs.Position;

namespace Writer.Scenes;

/// <summary>Builds a dense visual stress scene with 1000 drawable rectangles.</summary>
public static class StressScene
{
    private const int LayerCount = 10;
    private const int RectCount = 1000;

    public static void Build(EntityStore entities, Vector2 viewportSize)
    {
        if (viewportSize.X <= 0 || viewportSize.Y <= 0)
            viewportSize = new Vector2(1280, 720);

        for (int layer = 0; layer < LayerCount; layer++)
            entities.CreateEntity(new VisualRid(), new Layer(layer), Tags.Get<Uninitialised>());

        var random = new RandomNumberGenerator { Seed = 12345 };

        for (int i = 0; i < RectCount; i++)
        {
            int layer = i % LayerCount;
            float width = random.RandfRange(8f, 40f);
            float height = random.RandfRange(8f, 40f);
            float x = random.RandfRange(width / 2, Mathf.Max(width / 2, viewportSize.X - width / 2));
            float y = random.RandfRange(height / 2, Mathf.Max(height / 2, viewportSize.Y - height / 2));

            entities.CreateEntity(
                new VisualRid(),
                new Rectangle(new Rect2(-width / 2, -height / 2, width, height)),
                new Drawable(layer),
                new Position(new Vector2(x, y)),
                new FillColor(Color.FromHsv(random.Randf(), 0.55f, 0.95f)),
                Tags.Get<Uninitialised, Dirty>());
        }
    }
}

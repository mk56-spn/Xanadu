using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Position = Writer.Ecs.Position;

namespace Writer.Scenes;

/// <summary>Builds 30 nested container trees for visual hierarchy testing.</summary>
public static class ContainerScene
{
    private const int TopLevelCount = 30;
    private const int Columns = 6;
    private const float ContainerWidth = 170;
    private const float ContainerHeight = 120;
    private const float Gap = 12;

    public static void Build(EntityStore entities, Vector2 viewportSize)
    {
        if (viewportSize.X <= 0 || viewportSize.Y <= 0)
            viewportSize = new Vector2(1280, 720);

        entities.CreateEntity(new VisualRid(), new Layer(0), Tags.Get<Uninitialised>());

        int rows = (TopLevelCount + Columns - 1) / Columns;
        float gridWidth = Columns * ContainerWidth + (Columns - 1) * Gap;
        float gridHeight = rows * ContainerHeight + (rows - 1) * Gap;
        float startX = (viewportSize.X - gridWidth) / 2 + ContainerWidth / 2;
        float startY = (viewportSize.Y - gridHeight) / 2 + ContainerHeight / 2;

        for (int i = 0; i < TopLevelCount; i++)
        {
            int column = i % Columns;
            int row = i / Columns;
            var center = new Vector2(
                startX + column * (ContainerWidth + Gap),
                startY + row * (ContainerHeight + Gap));

            BuildTree(
                entities,
                parent: null,
                center,
                new Vector2(ContainerWidth, ContainerHeight),
                depth: 1 + i % 4,
                seed: i);
        }
    }

    private static void BuildTree(
        EntityStore entities,
        Entity? parent,
        Vector2 center,
        Vector2 size,
        int depth,
        int seed)
    {
        var direction = (seed + depth) % 2 == 0
            ? StackDirection.Horizontal
            : StackDirection.Vertical;

        var container = entities.CreateEntity(
            new VisualRid(),
            new Rectangle(new Rect2(-size / 2, size)),
            new Drawable(0),
            new Position(parent is null ? center : Vector2.Zero),
            new FillColor(Color.FromHsv((seed * 0.07f) % 1f, 0.45f, 0.25f + depth * 0.08f)),
            new Text(depth.ToString()),
            new UiContainer(direction, spacing: 4, clip: true),
            Tags.Get<Uninitialised, Dirty, LayoutDirty>());

        if (parent is { } parentEntity)
            parentEntity.AddChild(container);

        if (depth <= 1)
            return;

        BuildTree(
            entities,
            container,
            Vector2.Zero,
            size * 0.72f,
            depth - 1,
            seed + 1);
    }
}

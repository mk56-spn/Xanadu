using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Position = Writer.Ecs.Position;

namespace Writer.Scenes;

/// <summary>Builds 30 nested container trees with varying child counts.</summary>
public static class ContainerScene
{
    private const int TopLevelCount = 30;
    private const int Columns = 6;
    private const float ContainerWidth = 170;
    private const float ContainerHeight = 120;
    private const float Gap = 12;
    private const float Spacing = 4;
    private const float ChildInset = 0.8f;

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
                depth: 1 + i % 3,
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
        int childCount = 1 + (seed + depth) % 3;
        StackDirection direction = (seed + depth) % 2 == 0
            ? StackDirection.Horizontal
            : StackDirection.Vertical;

        Entity container = entities.CreateEntity(
            new VisualRid(),
            new Rectangle(new Rect2(-size / 2, size)),
            new Drawable(0),
            new Position(parent is null ? center : Vector2.Zero),
            new FillColor(Color.FromHsv((seed * 0.07f) % 1f, 0.45f, 0.25f + depth * 0.08f)),
            new UiContainer(direction, Spacing, clip: true),
            Tags.Get<Uninitialised, Dirty, LayoutDirty>());

        if (parent is { } parentEntity)
            parentEntity.AddChild(container);

        Vector2 childSize = GetChildSize(size, direction, childCount);
        bool canNest = depth > 1 && size.X >= 60 && size.Y >= 44;

        for (int childIndex = 0; childIndex < childCount; childIndex++)
        {
            if (canNest && childIndex == 0)
            {
                BuildTree(
                    entities,
                    container,
                    Vector2.Zero,
                    childSize,
                    depth - 1,
                    seed + childIndex + 1);
            }
            else
            {
                AddLeaf(
                    entities,
                    container,
                    childSize,
                    Color.FromHsv(((seed + childIndex) * 0.13f) % 1f, 0.5f, 0.75f),
                    childIndex + 1);
            }
        }
    }

    private static void AddLeaf(
        EntityStore entities,
        Entity parent,
        Vector2 size,
        Color color,
        int index)
    {
        Tags tags = Tags.Get<Uninitialised, Dirty, TakesInput>();
        Entity leaf = size.X >= 24 && size.Y >= 18
            ? entities.CreateEntity(
                new VisualRid(),
                new Rectangle(new Rect2(-size / 2, size)),
                new Drawable(0),
                new Position(Vector2.Zero),
                new FillColor(color),
                new Text(index.ToString()),
                tags)
            : entities.CreateEntity(
                new VisualRid(),
                new Rectangle(new Rect2(-size / 2, size)),
                new Drawable(0),
                new Position(Vector2.Zero),
                new FillColor(color),
                tags);

        parent.AddChild(leaf);
    }

    private static Vector2 GetChildSize(Vector2 parentSize, StackDirection direction, int childCount)
    {
        Vector2 inner = parentSize * ChildInset;
        float spacing = Spacing * (childCount - 1);

        return direction == StackDirection.Horizontal
            ? new Vector2(Mathf.Max(1, (inner.X - spacing) / childCount), inner.Y)
            : new Vector2(inner.X, Mathf.Max(1, (inner.Y - spacing) / childCount));
    }
}

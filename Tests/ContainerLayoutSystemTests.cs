using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Systems.Layout;
using Position = Writer.Ecs.Position;

namespace Writer.Tests;

[TestFixture]
public sealed class ContainerLayoutSystemTests
{
    private EntityStore _store = null!;
    private SystemRoot _root = null!;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _root = new SystemRoot(_store);
        _root.Add(new ContainerLayoutSystem());
    }

    [Test]
    public void HorizontalContainerCentersChildren()
    {
        var container = AddContainer(StackDirection.Horizontal, spacing: 10);
        var first = AddChild(container, new Vector2(40, 20));
        var second = AddChild(container, new Vector2(60, 20));

        Frame();

        Assert.That(first.GetComponent<Position>().Value, Is.EqualTo(new Vector2(-35, 0)));
        Assert.That(second.GetComponent<Position>().Value, Is.EqualTo(new Vector2(25, 0)));
        Assert.That(container.Tags.Has<LayoutDirty>(), Is.False);
    }

    [Test]
    public void VerticalContainerCentersChildren()
    {
        var container = AddContainer(StackDirection.Vertical, spacing: 4);
        var first = AddChild(container, new Vector2(20, 20));
        var second = AddChild(container, new Vector2(20, 30));

        Frame();

        Assert.That(first.GetComponent<Position>().Value, Is.EqualTo(new Vector2(0, -17)));
        Assert.That(second.GetComponent<Position>().Value, Is.EqualTo(new Vector2(0, 12)));
    }

    private Entity AddContainer(StackDirection direction, float spacing) =>
        _store.CreateEntity(
            new Rectangle(new Rect2(-100, -50, 200, 100)),
            new UiContainer(direction, spacing),
            Tags.Get<LayoutDirty>());

    private Entity AddChild(Entity container, Vector2 size)
    {
        var child = _store.CreateEntity(
            new Rectangle(new Rect2(-size / 2, size)),
            new Position(Vector2.Zero));

        container.AddChild(child);
        return child;
    }

    private void Frame() => _root.Update(new UpdateTick(1f / 60f, 0));
}

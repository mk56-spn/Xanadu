using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Systems.Layout;
using Position = Writer.Ecs.Position;

namespace Writer.Tests.Layout;

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

    [Test]
    public void CrossAlignmentMovesChildrenToContainerEdges()
    {
        var container = _store.CreateEntity(
            new Rectangle(new Rect2(-100, -50, 200, 100)),
            new UiContainer(
                StackDirection.Vertical,
                cross: StackCrossAlignment.Start),
            Tags.Get<LayoutDirty>());
        var narrow = AddChild(container, new Vector2(20, 20));
        var wide = AddChild(container, new Vector2(80, 20));

        Frame();

        Assert.That(
            narrow.GetComponent<Position>().Value.X,
            Is.EqualTo(-90));
        Assert.That(wide.GetComponent<Position>().Value.X, Is.EqualTo(-60));
    }

    [Test]
    public void AutoSizeMeasuresChildrenWithPadding()
    {
        var container = _store.CreateEntity(
            new Rectangle(new Rect2(-10, -10, 20, 20)),
            new UiContainer(
                StackDirection.Horizontal,
                spacing: 10,
                padding: 10,
                autoWidth: true,
                autoHeight: true),
            Tags.Get<LayoutDirty>());
        AddChild(container, new Vector2(40, 20));
        AddChild(container, new Vector2(60, 20));

        Frame();

        Assert.That(
            container.GetComponent<Rectangle>().Bounds.Size,
            Is.EqualTo(new Vector2(130, 40)));
    }

    [Test]
    public void AnchorTopKeepsTheContainerTopFixedWhileItGrows()
    {
        var container = _store.CreateEntity(
            new Rectangle(new Rect2(-50, -5, 100, 10)),
            new Position(new Vector2(0, 100)),
            new UiContainer(
                StackDirection.Vertical,
                padding: 5,
                autoHeight: true,
                anchorTop: true),
            Tags.Get<LayoutDirty>());
        AddChild(container, new Vector2(20, 30));

        Frame();

        Assert.That(
            container.GetComponent<Rectangle>().Bounds.Size.Y,
            Is.EqualTo(40));

        float top = container.GetComponent<Position>().Value.Y +
                    container.GetComponent<Rectangle>().Bounds.Position.Y;
        Assert.That(top, Is.EqualTo(95));
    }

    [Test]
    public void LayoutIgnoreExcludesAChildFromTheStack()
    {
        var container = AddContainer(StackDirection.Horizontal, spacing: 10);
        var first = AddChild(container, new Vector2(40, 20));
        var hidden = AddChild(container, new Vector2(100, 20));
        var second = AddChild(container, new Vector2(60, 20));
        hidden.AddTag<LayoutIgnore>();
        hidden.GetComponent<Position>().Value = UiLayout.Hidden;

        Frame();

        Assert.That(first.GetComponent<Position>().Value.X, Is.EqualTo(-35));
        Assert.That(second.GetComponent<Position>().Value.X, Is.EqualTo(25));
        Assert.That(
            hidden.GetComponent<Position>().Value,
            Is.EqualTo(UiLayout.Hidden));
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

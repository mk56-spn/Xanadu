using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Tests.Support;
using Writer.Systems.Layout;
using Position = Writer.Ecs.Position;

namespace Writer.Tests.UI.Layout;

[TestFixture]
public sealed class ContainerLayoutSystemTests : EcsSystemTest
{
    protected override void OnEcsSetUp()
    {
        Root.Add(new ContainerLayoutSystem());
    }

    [Test]
    public void HorizontalContainerCentersChildren()
    {
        Entity container = AddContainer(StackDirection.Horizontal, spacing: 10);
        Entity first = AddChild(container, new Vector2(40, 20));
        Entity second = AddChild(container, new Vector2(60, 20));

        Frame();

        Assert.That(first.GetComponent<Position>().Value, Is.EqualTo(new Vector2(-35, 0)));
        Assert.That(second.GetComponent<Position>().Value, Is.EqualTo(new Vector2(25, 0)));
        Assert.That(container.Tags.Has<LayoutDirty>(), Is.False);
    }

    [Test]
    public void VerticalContainerCentersChildren()
    {
        Entity container = AddContainer(StackDirection.Vertical, spacing: 4);
        Entity first = AddChild(container, new Vector2(20, 20));
        Entity second = AddChild(container, new Vector2(20, 30));

        Frame();

        Assert.That(first.GetComponent<Position>().Value, Is.EqualTo(new Vector2(0, -17)));
        Assert.That(second.GetComponent<Position>().Value, Is.EqualTo(new Vector2(0, 12)));
    }

    [Test]
    public void CrossAlignmentMovesChildrenToContainerEdges()
    {
        Entity container = Store.CreateEntity(
            new Rectangle(new Rect2(-100, -50, 200, 100)),
            new UiContainer(
                StackDirection.Vertical,
                cross: StackCrossAlignment.Start),
            Tags.Get<LayoutDirty>());
        Entity narrow = AddChild(container, new Vector2(20, 20));
        Entity wide = AddChild(container, new Vector2(80, 20));

        Frame();

        Assert.That(
            narrow.GetComponent<Position>().Value.X,
            Is.EqualTo(-90));
        Assert.That(wide.GetComponent<Position>().Value.X, Is.EqualTo(-60));
    }

    [Test]
    public void AutoSizeMeasuresChildrenWithPadding()
    {
        Entity container = Store.CreateEntity(
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
        Entity container = Store.CreateEntity(
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
        Entity container = AddContainer(StackDirection.Horizontal, spacing: 10);
        Entity first = AddChild(container, new Vector2(40, 20));
        Entity hidden = AddChild(container, new Vector2(100, 20));
        Entity second = AddChild(container, new Vector2(60, 20));
        hidden.AddTag<LayoutIgnore>();
        hidden.GetComponent<Position>().Value = UiLayout.Hidden;

        Frame();

        Assert.That(first.GetComponent<Position>().Value.X, Is.EqualTo(-35));
        Assert.That(second.GetComponent<Position>().Value.X, Is.EqualTo(25));
        Assert.That(
            hidden.GetComponent<Position>().Value,
            Is.EqualTo(UiLayout.Hidden));
    }

    [Test]
    public void NestedHeaderMiddleFooterRegionsLayoutInOrder()
    {
        Entity root = Store.CreateEntity(
            new Rectangle(new Rect2(-200, -150, 400, 300)),
            new Position(new Vector2(200, 150)),
            new UiContainer(
                StackDirection.Vertical,
                spacing: 10,
                padding: 10,
                cross: StackCrossAlignment.Start),
            Tags.Get<LayoutDirty>());
        Entity header = AddNestedContainer(
            root, StackDirection.Horizontal, new Vector2(380, 60), spacing: 5);
        Entity middle = AddNestedContainer(
            root, StackDirection.Horizontal, new Vector2(380, 160));
        Entity footer = AddNestedContainer(
            root, StackDirection.Vertical, new Vector2(380, 40));

        Entity title = AddChild(header, new Vector2(100, 20));
        Entity menu = AddChild(header, new Vector2(100, 20));
        Entity field = AddChild(middle, new Vector2(200, 120));
        Entity sidebar = AddChild(middle, new Vector2(160, 120));
        Entity transport = AddChild(footer, new Vector2(380, 30));

        Frame();

        Assert.That(header.GetComponent<Position>().Value,
            Is.EqualTo(new Vector2(0, -110)));
        Assert.That(middle.GetComponent<Position>().Value,
            Is.EqualTo(new Vector2(0, 10)));
        Assert.That(footer.GetComponent<Position>().Value,
            Is.EqualTo(new Vector2(0, 120)));

        Assert.That(title.GetComponent<Position>().Value.X,
            Is.EqualTo(-52.5f));
        Assert.That(menu.GetComponent<Position>().Value.X,
            Is.EqualTo(52.5f));
        Assert.That(field.GetComponent<Position>().Value.X,
            Is.EqualTo(-80));
        Assert.That(sidebar.GetComponent<Position>().Value.X,
            Is.EqualTo(100));
        Assert.That(transport.GetComponent<Position>().Value.Y,
            Is.EqualTo(0));
        Assert.That(root.GetComponent<Rectangle>().Bounds.Size,
            Is.EqualTo(new Vector2(400, 300)));
    }

    private Entity AddNestedContainer(
        Entity parent,
        StackDirection direction,
        Vector2 size,
        float spacing = 0)
    {
        Entity container = Store.CreateEntity(
            new Rectangle(new Rect2(-size / 2, size)),
            new Position(Vector2.Zero),
            new UiContainer(direction, spacing),
            Tags.Get<LayoutDirty>());
        parent.AddChild(container);
        return container;
    }

    private Entity AddContainer(StackDirection direction, float spacing) =>
        Store.CreateEntity(
            new Rectangle(new Rect2(-100, -50, 200, 100)),
            new UiContainer(direction, spacing),
            Tags.Get<LayoutDirty>());

    private Entity AddChild(Entity container, Vector2 size)
    {
        Entity child = Store.CreateEntity(
            new Rectangle(new Rect2(-size / 2, size)),
            new Position(Vector2.Zero));

        container.AddChild(child);
        return child;
    }
}

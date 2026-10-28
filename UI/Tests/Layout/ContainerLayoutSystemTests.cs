using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Tests.Support;
using Xanadu.Systems.Layout;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.UI.Layout;

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
    public void MainAlignmentStartPacksChildrenAtTheLeadingEdgeInsidePadding()
    {
        Entity container = Store.CreateEntity(
            new Rectangle(new Rect2(-100, -50, 200, 100)),
            new UiContainer(
                StackDirection.Vertical,
                spacing: 4,
                padding: 6,
                main: StackMainAlignment.Start),
            Tags.Get<LayoutDirty>());
        Entity first = AddChild(container, new Vector2(20, 20));
        Entity second = AddChild(container, new Vector2(20, 20));

        Frame();

        Assert.That(first.GetComponent<Position>().Value.Y, Is.EqualTo(-50 + 6 + 10));
        Assert.That(second.GetComponent<Position>().Value.Y, Is.EqualTo(-50 + 6 + 20 + 4 + 10));
    }

    [Test]
    public void MainAlignmentEndPacksChildrenAtTheTrailingEdgeInsidePadding()
    {
        Entity container = Store.CreateEntity(
            new Rectangle(new Rect2(-100, -50, 200, 100)),
            new UiContainer(
                StackDirection.Horizontal,
                spacing: 4,
                padding: 6,
                main: StackMainAlignment.End),
            Tags.Get<LayoutDirty>());
        Entity first = AddChild(container, new Vector2(20, 20));
        Entity second = AddChild(container, new Vector2(30, 20));

        Frame();

        Assert.That(second.GetComponent<Position>().Value.X, Is.EqualTo(100 - 6 - 15));
        Assert.That(first.GetComponent<Position>().Value.X, Is.EqualTo(100 - 6 - 30 - 4 - 10));
    }

    [Test]
    public void StartAlignmentKeepsRowsStillWhenALaterRowIsRemoved()
    {
        Entity container = Store.CreateEntity(
            new Rectangle(new Rect2(-100, -50, 200, 100)),
            new UiContainer(StackDirection.Vertical, main: StackMainAlignment.Start),
            Tags.Get<LayoutDirty>());
        Entity first = AddChild(container, new Vector2(20, 20));
        Entity second = AddChild(container, new Vector2(20, 20));
        Frame();
        float before = first.GetComponent<Position>().Value.Y;

        second.AddTag<LayoutIgnore>();
        container.AddTag<LayoutDirty>();
        Frame();

        Assert.That(first.GetComponent<Position>().Value.Y, Is.EqualTo(before));
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
    public void OpenDropdownReservesMainAxisSpace()
    {
        Entity container = Store.CreateEntity(
            new Rectangle(new Rect2(-100, -75, 200, 150)),
            new UiContainer(
                StackDirection.Vertical,
                autoHeight: true),
            Tags.Get<LayoutDirty>());
        Entity first = AddChild(container, new Vector2(100, 30));
        first.AddComponent(new Dropdown(
            default,
            0,
            new Vector2(100, 80))
        {
            Open = true,
        });
        Entity second = AddChild(container, new Vector2(100, 30));

        Frame();

        Assert.That(
            container.GetComponent<Rectangle>().Bounds.Size.Y,
            Is.EqualTo(144));
        Assert.That(second.GetComponent<Position>().Value.Y,
            Is.EqualTo(57));
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

    [Test]
    public void SkewedVerticalStackFlowsChildrenAlongTheSlantedSide()
    {
        float skew = Mathf.DegToRad(6);
        Entity container = Store.CreateEntity(
            new Rectangle(new Rect2(-100, -50, 200, 100)),
            new UiContainer(StackDirection.Vertical, spacing: 10, skew: skew),
            Tags.Get<LayoutDirty>());
        Entity upper = AddChild(container, new Vector2(20, 20));
        Entity lower = AddChild(container, new Vector2(20, 20));

        Frame();

        Vector2 up = upper.GetComponent<Position>().Value;
        Vector2 down = lower.GetComponent<Position>().Value;
        Assert.That(up.Y, Is.LessThan(down.Y));
        Assert.That(up.X, Is.EqualTo(-Mathf.Tan(skew) * up.Y).Within(0.001f));
        Assert.That(up.X, Is.GreaterThan(down.X));
    }

    [Test]
    public void AutoWidthWithAnchorLeftGrowsRightwards()
    {
        Entity container = Store.CreateEntity(
            new Rectangle(new Rect2(-50, -25, 100, 50)),
            new Position(new Vector2(200, 0)),
            new UiContainer(
                StackDirection.Horizontal,
                autoWidth: true,
                anchorLeft: true),
            Tags.Get<LayoutDirty>());
        AddChild(container, new Vector2(300, 20));

        Frame();

        Rect2 bounds = container.GetComponent<Rectangle>().Bounds;
        float left = container.GetComponent<Position>().Value.X + bounds.Position.X;
        Assert.That(bounds.Size.X, Is.EqualTo(300));
        Assert.That(left, Is.EqualTo(150).Within(0.001f));
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

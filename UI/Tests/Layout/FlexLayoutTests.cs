using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Tests.Support;
using Xanadu.Systems.Layout;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.UI.Layout;

/// <summary>Grow, stretch, min and max (<see cref="UiFlex"/>) in a stack.</summary>
[TestFixture]
public sealed class FlexLayoutTests : EcsSystemTest
{
    protected override void OnEcsSetUp()
    {
        Root.Add(new ContainerLayoutSystem());
    }

    [Test]
    public void GrowingChildTakesTheRoomAFixedSiblingLeaves()
    {
        Entity row = AddContainer(StackDirection.Horizontal, new Vector2(1000, 100), spacing: 20, padding: 10);
        Entity panel = AddChild(row, new Vector2(300, 50));
        Entity slot = AddChild(row, Vector2.Zero, new UiFlex(Vector2.Zero) { Grow = 1 });

        Frame();

        Assert.That(Size(slot).X, Is.EqualTo(1000 - 20 - 20 - 300));
        Assert.That(Left(panel), Is.EqualTo(-490));
        Assert.That(Left(slot), Is.EqualTo(-490 + 300 + 20));
    }

    [Test]
    public void GrowSharesByWeight()
    {
        Entity row = AddContainer(StackDirection.Horizontal, new Vector2(300, 100));
        Entity one = AddChild(row, Vector2.Zero, new UiFlex(Vector2.Zero) { Grow = 1 });
        Entity two = AddChild(row, Vector2.Zero, new UiFlex(Vector2.Zero) { Grow = 2 });

        Frame();

        Assert.That(Size(one).X, Is.EqualTo(100).Within(0.001f));
        Assert.That(Size(two).X, Is.EqualTo(200).Within(0.001f));
    }

    [Test]
    public void ACappedChildLeavesTheRestToItsSiblings()
    {
        Entity row = AddContainer(StackDirection.Horizontal, new Vector2(1000, 100));
        Entity capped = AddChild(row, Vector2.Zero, new UiFlex(Vector2.Zero) { Grow = 1, Max = new Vector2(200, 0) });
        Entity free = AddChild(row, Vector2.Zero, new UiFlex(Vector2.Zero) { Grow = 1 });

        Frame();

        Assert.That(Size(capped).X, Is.EqualTo(200));
        Assert.That(Size(free).X, Is.EqualTo(800).Within(0.001f));
    }

    [Test]
    public void EqualTabsCapAndPackAtTheStart()
    {
        Entity strip = AddContainer(StackDirection.Horizontal, new Vector2(1000, 40), spacing: 10, main: StackMainAlignment.Start);
        var tabs = new Entity[3];
        for (int i = 0; i < tabs.Length; i++)
            tabs[i] = AddChild(strip, new Vector2(0, 40), new UiFlex(new Vector2(0, 40)) { Grow = 1, Max = new Vector2(200, 0) });

        Frame();

        foreach (Entity tab in tabs)
            Assert.That(Size(tab).X, Is.EqualTo(200));
        Assert.That(Left(tabs[0]), Is.EqualTo(-500));
        Assert.That(Left(tabs[2]), Is.EqualTo(-500 + 420));
    }

    [Test]
    public void StretchFillsTheCrossAxisInsidePadding()
    {
        Entity column = AddContainer(StackDirection.Vertical, new Vector2(400, 300), padding: 8);
        Entity stretched = AddChild(column, new Vector2(50, 20), new UiFlex(new Vector2(50, 20)) { Stretch = true });
        Entity plain = AddChild(column, new Vector2(50, 20));

        Frame();

        Assert.That(Size(stretched), Is.EqualTo(new Vector2(384, 20)));
        Assert.That(Size(plain), Is.EqualTo(new Vector2(50, 20)));
    }

    [Test]
    public void StretchAlignmentStretchesEveryChildAndRemembersItsSize()
    {
        Entity column = Store.CreateEntity(
            new Rectangle(new Rect2(-200, -150, 400, 300)),
            new UiContainer(StackDirection.Vertical, cross: StackCrossAlignment.Stretch),
            Tags.Get<LayoutDirty>());
        Entity child = AddChild(column, new Vector2(50, 20));

        Frame();

        Assert.That(Size(child), Is.EqualTo(new Vector2(400, 20)));
        Assert.That(child.GetComponent<UiFlex>().Natural, Is.EqualTo(new Vector2(50, 20)));
    }

    [Test]
    public void MinHoldsAStretchedChildAboveItsRoom()
    {
        Entity column = AddContainer(StackDirection.Vertical, new Vector2(100, 300));
        Entity child = AddChild(column, new Vector2(50, 20), new UiFlex(new Vector2(50, 20)) { Stretch = true, Min = new Vector2(150, 0) });

        Frame();

        Assert.That(Size(child).X, Is.EqualTo(150));
    }

    [Test]
    public void AResizedContainerChildPlacesItsOwnChildrenTheSameFrame()
    {
        Entity page = AddContainer(StackDirection.Vertical, new Vector2(1000, 600), main: StackMainAlignment.Start);
        Entity body = Store.CreateEntity(
            new Rectangle(new Rect2(0, 0, 0, 0)),
            new Position(Vector2.Zero),
            new UiContainer(StackDirection.Horizontal, main: StackMainAlignment.Start),
            new UiFlex(Vector2.Zero) { Grow = 1, Stretch = true });
        page.AddChild(body);
        Entity panel = AddChild(body, new Vector2(300, 10), new UiFlex(new Vector2(300, 10)) { Stretch = true });
        Entity slot = AddChild(body, Vector2.Zero, new UiFlex(Vector2.Zero) { Grow = 1, Stretch = true });

        Frame();

        Assert.That(Size(body), Is.EqualTo(new Vector2(1000, 600)));
        Assert.That(Size(panel), Is.EqualTo(new Vector2(300, 600)));
        Assert.That(Size(slot), Is.EqualTo(new Vector2(700, 600)));
        Assert.That(Left(slot), Is.EqualTo(-200));
    }

    [Test]
    public void AnAutoSizedGrowingChildStartsFromItsContent()
    {
        Entity row = AddContainer(StackDirection.Horizontal, new Vector2(500, 100));
        Entity inner = Store.CreateEntity(
            new Rectangle(new Rect2(0, 0, 0, 0)),
            new Position(Vector2.Zero),
            new UiContainer(StackDirection.Horizontal, autoWidth: true, autoHeight: true),
            new UiFlex(Vector2.Zero) { Grow = 1 },
            Tags.Get<LayoutDirty>());
        row.AddChild(inner);
        AddChild(inner, new Vector2(120, 30));
        Entity fixedChild = AddChild(row, new Vector2(100, 30));

        Frame();
        Frame();

        Assert.That(inner.GetComponent<UiFlex>().Natural, Is.EqualTo(new Vector2(120, 30)));
        Assert.That(Size(inner), Is.EqualTo(new Vector2(400, 30)));
        Assert.That(Size(fixedChild), Is.EqualTo(new Vector2(100, 30)));
    }

    private static Vector2 Size(Entity entity) => entity.GetComponent<Rectangle>().Bounds.Size;

    private static float Left(Entity entity) =>
        entity.GetComponent<Position>().Value.X - Size(entity).X / 2;

    private Entity AddContainer(
        StackDirection direction,
        Vector2 size,
        float spacing = 0,
        float padding = 0,
        StackMainAlignment main = StackMainAlignment.Center) =>
        Store.CreateEntity(
            new Rectangle(new Rect2(-size / 2, size)),
            new UiContainer(direction, spacing, padding: padding, main: main),
            Tags.Get<LayoutDirty>());

    private Entity AddChild(Entity container, Vector2 size, UiFlex? flex = null)
    {
        Entity child = Store.CreateEntity(
            new Rectangle(new Rect2(-size / 2, size)),
            new Position(Vector2.Zero));
        if (flex is { } value)
            child.AddComponent(value);

        container.AddChild(child);
        return child;
    }
}

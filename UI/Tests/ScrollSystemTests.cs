using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.UI;

[TestFixture]
public sealed class ScrollSystemTests
{
    [Test]
    public void WheelScrollsContentAndClampsToTheEnd()
    {
        var store = new EntityStore();
        Entity content = store.CreateEntity(
            new Position(new Vector2(0, 400)),
            new Rectangle(new Rect2(-100, -500, 200, 1000)));
        Entity viewport = store.CreateEntity(
            new Position(new Vector2(500, 300)),
            new Rectangle(new Rect2(-100, -100, 200, 200)),
            new ScrollView(content));

        var scroll = new ScrollSystem();
        var root = new SystemRoot(store);
        root.Add(scroll);

        scroll.Request(new Vector2(500, 300), 1);
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(viewport.GetComponent<ScrollView>().Offset,
            Is.EqualTo(48));
        Assert.That(content.GetComponent<Position>().Value.Y,
            Is.EqualTo(352));

        for (int i = 0; i < 30; i++)
            scroll.Request(new Vector2(500, 300), 1);
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(viewport.GetComponent<ScrollView>().Offset,
            Is.EqualTo(800));
        Assert.That(content.GetComponent<Position>().Value.Y,
            Is.EqualTo(-400));
    }

    [TestCase(1, 5)]
    [TestCase(5, 1)]
    public void TopmostLayerTakesTheWheel(int firstZ, int secondZ)
    {
        var store = new EntityStore();
        Entity first = Viewport(store, firstZ);
        Entity second = Viewport(store, secondZ);

        var scroll = new ScrollSystem();
        var root = new SystemRoot(store);
        root.Add(scroll);

        scroll.Request(new Vector2(500, 300), 1);
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(first.GetComponent<ScrollView>().Offset, Is.EqualTo(firstZ > secondZ ? 48 : 0));
        Assert.That(second.GetComponent<ScrollView>().Offset, Is.EqualTo(secondZ > firstZ ? 48 : 0));
    }

    private static Entity Viewport(EntityStore store, int layerZ)
    {
        Entity layer = store.CreateEntity(new Layer(layerZ));
        Entity content = store.CreateEntity(
            new Position(Vector2.Zero),
            new Rectangle(new Rect2(-100, -500, 200, 1000)));
        Entity viewport = store.CreateEntity(
            new Position(new Vector2(500, 300)),
            new Rectangle(new Rect2(-100, -100, 200, 200)),
            new ScrollView(content));
        layer.AddChild(viewport);
        return viewport;
    }
}

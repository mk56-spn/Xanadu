using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Systems.Ui;
using Position = Writer.Ecs.Position;

namespace Writer.Tests.UI;

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
}

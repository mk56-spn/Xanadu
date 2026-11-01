using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Input;
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
            new ScrollView());
        viewport.AddChild(content);

        var pointer = new Pointer();
        var root = new SystemRoot(store);
        root.Add(new ScrollSystem(pointer, pointer));

        pointer.Notches = -1;
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(viewport.GetComponent<ScrollView>().Offset,
            Is.EqualTo(48));
        Assert.That(content.GetComponent<Position>().Value.Y,
            Is.EqualTo(352));

        pointer.Notches = -30;
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

        var pointer = new Pointer();
        var root = new SystemRoot(store);
        root.Add(new ScrollSystem(pointer, pointer));

        pointer.Notches = -1;
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(first.GetComponent<ScrollView>().Offset, Is.EqualTo(firstZ > secondZ ? 48 : 0));
        Assert.That(second.GetComponent<ScrollView>().Offset, Is.EqualTo(secondZ > firstZ ? 48 : 0));
    }

    [Test]
    public void AViewportUnderThePointerTakesTheWheel()
    {
        var store = new EntityStore();
        Viewport(store, 1);
        var pointer = new Pointer { Notches = -1 };
        var root = new SystemRoot(store);
        root.Add(new ScrollSystem(pointer, pointer));

        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(pointer.Notches, Is.Zero);
    }

    [Test]
    public void WithNoViewportUnderThePointerTheWheelIsLeftForTheHost()
    {
        var store = new EntityStore();
        Entity viewport = Viewport(store, 1);
        var pointer = new Pointer { Notches = -1, Position = new Vector2(1500, 900) };
        var root = new SystemRoot(store);
        root.Add(new ScrollSystem(pointer, pointer));

        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(pointer.Notches, Is.EqualTo(-1));
        Assert.That(viewport.GetComponent<ScrollView>().Offset, Is.Zero);
    }

    private sealed class Pointer : IPointerInputSource, IWheelSource
    {
        public Vector2 Position = new(500, 300);

        public int Notches { get; set; }

        public bool Control => false;

        public bool Shift => false;

        public PointerInput Read() => new(Position, false);

        public int TakeWheel()
        {
            int notches = Notches;
            Notches = 0;
            return notches;
        }
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
            new ScrollView());
        viewport.AddChild(content);
        layer.AddChild(viewport);
        return viewport;
    }
}

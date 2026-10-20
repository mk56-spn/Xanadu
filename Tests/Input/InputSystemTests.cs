using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Systems.Input;
using Writer.Systems.Layout;
using Position = Writer.Ecs.Position;

namespace Writer.Tests.Input;

[TestFixture]
public sealed class InputSystemTests
{
    private EntityStore _store = null!;
    private ScriptedPointerInput _input = null!;
    private SystemRoot _root = null!;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _input = new ScriptedPointerInput();
        _root = new SystemRoot(_store) { new ContainerLayoutSystem(), new InputSystem(_input) };
    }

    [Test]
    public void TopLayerGetsPressed()
    {
        Entity bottom = AddInputRect(new Vector2(100, 100), new Vector2(100, 100), 0);
        Entity top = AddInputRect(new Vector2(100, 100), new Vector2(100, 100), 5);

        Press(new Vector2(100, 100));

        Assert.That(bottom.Tags.Has<Pressed>(), Is.False);
        Assert.That(top.Tags.Has<Pressed>(), Is.True);
    }

    [Test]
    public void NonInputRectDoesNotBlockInputRect()
    {
        Entity input = AddInputRect(new Vector2(100, 100), new Vector2(100, 100), 0);
        AddInputRect(new Vector2(100, 100), new Vector2(100, 100), 10, takesInput: false);

        Press(new Vector2(100, 100));

        Assert.That(input.Tags.Has<Pressed>(), Is.True);
    }

    [Test]
    public void HitTestUsesCenteredRectangleBounds()
    {
        Entity input = AddInputRect(new Vector2(300, 200), new Vector2(100, 50), 0);

        Press(new Vector2(349, 224));
        Assert.That(input.Tags.Has<Pressed>(), Is.True);

        Release(new Vector2(349, 224));
        Assert.That(input.Tags.Has<Pressed>(), Is.False);

        Press(new Vector2(351, 224));
        Assert.That(input.Tags.Has<Pressed>(), Is.False);
    }

    [Test]
    public void PressOutsideInputDoesNothing()
    {
        Entity input = AddInputRect(new Vector2(100, 100), new Vector2(100, 100), 0);

        Press(new Vector2(0, 0));

        Assert.That(input.Tags.Has<Pressed>(), Is.False);
    }

    [Test]
    public void ReleaseClearsPressed()
    {
        Entity input = AddInputRect(new Vector2(100, 100), new Vector2(100, 100), 0);

        Press(new Vector2(100, 100));
        Assert.That(input.Tags.Has<Pressed>(), Is.True);

        Release(new Vector2(100, 100));
        Assert.That(input.Tags.Has<Pressed>(), Is.False);
    }

    [Test]
    public void SameLayerUsesHighestEntityId()
    {
        Entity first = AddInputRect(new Vector2(100, 100), new Vector2(100, 100), 0);
        Entity second = AddInputRect(new Vector2(100, 100), new Vector2(100, 100), 0);

        Press(new Vector2(100, 100));

        Assert.That(first.Tags.Has<Pressed>(), Is.False);
        Assert.That(second.Tags.Has<Pressed>(), Is.True);
    }

    [Test]
    public void ChildInputUsesContainerWorldPosition()
    {
        Entity container = _store.CreateEntity(new Position(new Vector2(200, 200)));
        Entity child = AddInputRect(new Vector2(50, 0), new Vector2(40, 40), 0);
        container.AddChild(child);

        Press(new Vector2(250, 200));

        Assert.That(child.Tags.Has<Pressed>(), Is.True);
    }

    [Test]
    public void NestedContainerInputUsesAccumulatedWorldPosition()
    {
        Entity outer = _store.CreateEntity(new Position(new Vector2(200, 100)));
        Entity inner = _store.CreateEntity(new Position(new Vector2(50, 25)));
        outer.AddChild(inner);

        Entity child = AddInputRect(new Vector2(10, 5), new Vector2(20, 20), 0);
        inner.AddChild(child);

        Press(new Vector2(260, 130));

        Assert.That(child.Tags.Has<Pressed>(), Is.True);
    }

    [Test]
    public void LayoutedChildInputUsesContainerWorldPosition()
    {
        Entity container = _store.CreateEntity(
            new Rectangle(new Rect2(-100, -50, 200, 100)),
            new UiContainer(StackDirection.Horizontal, 10),
            new Position(new Vector2(300, 200)),
            Tags.Get<LayoutDirty>());

        Entity first = AddInputRect(Vector2.Zero, new Vector2(40, 40), 0);
        Entity second = AddInputRect(Vector2.Zero, new Vector2(40, 40), 0);
        container.AddChild(first);
        container.AddChild(second);

        Press(new Vector2(275, 200));

        Assert.That(first.GetComponent<Position>().Value, Is.EqualTo(new Vector2(-25, 0)));
        Assert.That(second.GetComponent<Position>().Value, Is.EqualTo(new Vector2(25, 0)));
        Assert.That(first.Tags.Has<Pressed>(), Is.True);
        Assert.That(second.Tags.Has<Pressed>(), Is.False);
    }

    private Entity AddInputRect(Vector2 center, Vector2 size, int layer, bool takesInput = true)
    {
        Tags tags = takesInput ? Tags.Get<TakesInput>() : default;

        return _store.CreateEntity(
            new Rectangle(new Rect2(-size / 2, size)),
            new Drawable(layer),
            new Position(center),
            tags);
    }

    private void Press(Vector2 position)
    {
        _input.Current = new PointerInput(position, true);
        Frame();
    }

    private void Release(Vector2 position)
    {
        _input.Current = new PointerInput(position, false);
        Frame();
    }

    private void Frame() => _root.Update(new UpdateTick(1f / 60f, 0));

    private sealed class ScriptedPointerInput : IPointerInputSource
    {
        public PointerInput Current;

        public PointerInput Read() => Current;
    }
}

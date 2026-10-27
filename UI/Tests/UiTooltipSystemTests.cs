using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Input;
using Xanadu.Systems.Ui;
using Xanadu.Tests.Support;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.UI;

[TestFixture]
public sealed class UiTooltipSystemTests
{
    private EntityStore _store = null!;
    private Entity _target;
    private UiTooltipSystem _system = null!;
    private SystemRoot _root = null!;
    private FakePointer _pointer = null!;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        var visuals = new FakeVisualRidFactory();
        UiBuilder ui = UiBuilder.Begin(_store, visuals).Layer(10);
        _target = ui.RectEntity(
            new Vector2(120, 100),
            new Vector2(100, 32),
            Colors.Blue,
            "Target",
            takesInput: true);
        _target.AddComponent(new UiTooltip("A useful hint"));
        _target.AddTag<Hovered>();

        _pointer = new FakePointer { Position = new Vector2(130, 110) };
        _system = new UiTooltipSystem(
            _store,
            _pointer,
            visuals,
            new Vector2(800, 600));
        _root = new SystemRoot(_store);
        _root.Add(_system);
    }

    [Test]
    public void TheTooltipWaitsThenFollowsTheHoveredControl()
    {
        Frame(0.1f);
        Assert.That(TooltipText(), Is.Empty);

        Frame(0.4f);
        Assert.That(TooltipText(), Is.EqualTo("A useful hint"));
        Assert.That(_system.Tooltip.GetComponent<Position>().Value,
            Is.Not.EqualTo(UiLayout.Hidden));
    }

    [Test]
    public void PressingThePointerHidesTheTooltip()
    {
        Frame(0.5f);
        Assert.That(TooltipText(), Is.Not.Empty);

        _pointer.Pressed = true;
        Frame(0.1f);

        Assert.That(TooltipText(), Is.Empty);
        Assert.That(_system.Tooltip.GetComponent<Position>().Value,
            Is.EqualTo(UiLayout.Hidden));
    }

    [Test]
    public void ATooltipNearTheEdgeStaysOnScreen()
    {
        _pointer.Position = new Vector2(795, 595);
        Frame(0.5f);

        Rect2 area = new(
            _system.Tooltip.GetComponent<Position>().Value -
            _system.Tooltip.GetComponent<Rectangle>().Bounds.Size / 2,
            _system.Tooltip.GetComponent<Rectangle>().Bounds.Size);

        Assert.That(area.Position.X, Is.GreaterThanOrEqualTo(0f));
        Assert.That(area.Position.Y, Is.GreaterThanOrEqualTo(0f));
        Assert.That(area.End.X, Is.LessThanOrEqualTo(800f));
        Assert.That(area.End.Y, Is.LessThanOrEqualTo(600f));
    }

    private string TooltipText() =>
        _system.Tooltip.GetComponent<Text>().Value;

    private void Frame(float delta) =>
        _root.Update(new UpdateTick(delta, 0));

    private sealed class FakePointer : IPointerInputSource
    {
        public Vector2 Position;
        public bool Pressed;

        public PointerInput Read() => new(Position, Pressed);
    }
}

using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Tests.Support;
using Writer.Systems.Input;
using Writer.Systems.Ui;
using Position = Writer.Ecs.Position;

namespace Writer.Tests.UI;

[TestFixture]
public sealed class UiWheelTests : EcsSystemTest
{
    private ScriptedPointer _pointer = null!;

    protected override void OnEcsSetUp()
    {
        _pointer = new ScriptedPointer();
        Root.Add(new InputSystem(_pointer));
        Root.Add(new UiWheelSystem(Store, _pointer));
    }

    [Test]
    public void WedgeZeroIsCentredUpAndWrapsClockwise()
    {
        Assert.That(UiWheelGeometry.IndexAt(new Vector2(0, -100), 8),
            Is.EqualTo(0));
        Assert.That(UiWheelGeometry.IndexAt(new Vector2(70, -70), 8),
            Is.EqualTo(1));
        Assert.That(UiWheelGeometry.IndexAt(new Vector2(100, 0), 8),
            Is.EqualTo(2));
        Assert.That(UiWheelGeometry.IndexAt(new Vector2(0, 100), 8),
            Is.EqualTo(4));
        Assert.That(UiWheelGeometry.IndexAt(new Vector2(-100, 0), 8),
            Is.EqualTo(6));
        Assert.That(UiWheelGeometry.IndexAt(new Vector2(-70, -70), 8),
            Is.EqualTo(7));
    }

    [Test]
    public void BuilderCreatesASelfPaintingWheel()
    {
        UiBuilder ui = UiBuilder.Begin(Store, new FakeVisualRidFactory()).Layer(0);

        UiWheelControl control = ui.Wheel(
            new Vector2(200, 200),
            90,
            new[] { "UP", "RIGHT", "DOWN", "LEFT" },
            selected: 1);

        Entity wheel = control.Wheel;
        Assert.That(wheel.HasComponent<UiWheel>(), Is.True);
        Assert.That(wheel.HasComponent<FillColor>(), Is.False);
        Assert.That(wheel.Tags.Has<Dirty>(), Is.True);
        Assert.That(control.Selected, Is.EqualTo(1));
        Assert.That(wheel.GetComponent<UiWheel>().Labels,
            Is.EqualTo(new[] { "UP", "RIGHT", "DOWN", "LEFT" }));
    }

    [Test]
    public void SelectingThroughTheControlRaisesOneChange()
    {
        Entity wheel = AddWheel(selected: 0);

        Assert.That(UiWheelControl.SetSelected(wheel, 3), Is.True);
        Assert.That(UiWheelControl.SelectedIndex(wheel), Is.EqualTo(3));
        Assert.That(wheel.GetComponent<UiWheelChanged>().Index, Is.EqualTo(3));
        Assert.That(UiWheelControl.SetSelected(wheel, 3), Is.False);
    }

    [Test]
    public void PointerPressSelectsAndDragSweepsWedgeByWedge()
    {
        Entity wheel = AddWheel(selected: 4);

        _pointer.Current = new PointerInput(new Vector2(200, 120), true);
        Frame(2);
        Assert.That(wheel.GetComponent<UiWheel>().Selected, Is.EqualTo(0));
        Assert.That(wheel.GetComponent<UiWheelChanged>().Index, Is.EqualTo(0));

        wheel.RemoveComponent<UiWheelChanged>();
        _pointer.Current = new PointerInput(new Vector2(260, 200), true);
        Frame();
        Assert.That(wheel.GetComponent<UiWheel>().Selected, Is.EqualTo(2));
        Assert.That(wheel.GetComponent<UiWheelChanged>().Index, Is.EqualTo(2));

        _pointer.Current = new PointerInput(new Vector2(140, 200), false);
        Frame();
        Assert.That(wheel.GetComponent<UiWheel>().Hover, Is.EqualTo(6));
    }

    [Test]
    public void HubPressDoesNotChangeTheSelection()
    {
        Entity wheel = AddWheel(selected: 3);

        _pointer.Current = new PointerInput(new Vector2(200, 200), true);
        Frame(2);

        Assert.That(wheel.GetComponent<UiWheel>().Selected, Is.EqualTo(3));
        Assert.That(wheel.HasComponent<UiWheelChanged>(), Is.False);
    }

    [Test]
    public void HiddenWheelIsNotHit()
    {
        Entity wheel = AddWheel(selected: 2);
        wheel.GetComponent<Position>().Value = UiLayout.Hidden;

        _pointer.Current = new PointerInput(new Vector2(200, 120), true);
        Frame(2);

        Assert.That(wheel.GetComponent<UiWheel>().Selected, Is.EqualTo(2));
        Assert.That(wheel.GetComponent<UiWheel>().Hover, Is.EqualTo(-1));
    }

    private Entity AddWheel(int selected) =>
        Store.CreateEntity(
            new Position(new Vector2(200, 200)),
            new Rectangle(new Rect2(-100, -100, 200, 200)),
            new Drawable(0),
            new UiWheel
            {
                Selected = selected,
                Hover = -1,
                Labels = new[] { "↑", "↗", "→", "↘", "↓", "↙", "←", "↖" },
                HubRadius = 0.28f,
                StartAngle = UiWheelGeometry.DefaultStartAngle,
                Gap = 0.04f,
            },
            Tags.Get<Dirty, TakesInput>());

    private sealed class ScriptedPointer : IPointerInputSource
    {
        public PointerInput Current;

        public PointerInput Read() => Current;
    }
}

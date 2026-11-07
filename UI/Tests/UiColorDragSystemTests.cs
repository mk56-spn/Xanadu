using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Input;
using Xanadu.Systems.Ui;
using Xanadu.Tests.Support;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.UI;

[TestFixture]
public sealed class UiColorDragSystemTests : EcsSystemTest
{
    private sealed class Pointer : IPointerInputSource
    {
        public PointerInput Current;
        public PointerInput Read() => Current;
    }

    private Pointer _pointer = null!;
    private UiColorDragSystem _drag = null!;
    private Entity _red;
    private Entity _blue;

    protected override void OnEcsSetUp()
    {
        _pointer = new Pointer();
        _red = Swatch(new Vector2(100, 100), Colors.Red);
        _blue = Swatch(new Vector2(300, 100), Colors.Blue);
        _drag = new UiColorDragSystem(Store, _pointer, new FakeVisualRidFactory());
        Root.Add(_drag);
    }

    private Entity Swatch(Vector2 at, Color color)
    {
        Entity swatch = Store.CreateEntity(
            new Rectangle(new Rect2(-14, -14, 28, 28)),
            new Position(at),
            new FillColor(color),
            new UiColorDrop());
        swatch.AddTag<UiColorSwatch>();
        return swatch;
    }

    private void Step(Vector2 at, bool pressed)
    {
        _pointer.Current = new PointerInput(at, pressed);
        Frame();
    }

    [Test]
    public void DraggingOneSwatchOntoAnotherHandsItTheColour()
    {
        Step(new Vector2(100, 100), true);
        Step(new Vector2(200, 100), true);
        Step(new Vector2(300, 100), true);
        Step(new Vector2(300, 100), false);

        Assert.That(UiColorDrop.Take(_blue, out Color color), Is.True);
        Assert.That(color, Is.EqualTo(Colors.Red));
        Assert.That(UiColorDrop.Take(_blue, out _), Is.False, "a drop is handed over once");
        Assert.That(UiColorDrop.Take(_red, out _), Is.False);
    }

    [Test]
    public void TheCarriedColourRidesBesideThePointerUntilLetGo()
    {
        Assert.That(UiLayout.IsHidden(_drag.Ghost), Is.True);

        Step(new Vector2(100, 100), true);
        Step(new Vector2(200, 150), true);

        Assert.That(UiLayout.IsHidden(_drag.Ghost), Is.False);
        Assert.That(_drag.Ghost.GetComponent<FillColor>().Value, Is.EqualTo(Colors.Red));
        Vector2 at = _drag.Ghost.GetComponent<Position>().Value;
        Assert.That(at.X, Is.GreaterThan(200f));
        Assert.That(at.Y, Is.GreaterThan(150f));

        Step(new Vector2(200, 150), false);
        Assert.That(UiLayout.IsHidden(_drag.Ghost), Is.True);
    }

    [Test]
    public void AClickWithoutMovingDropsNothing()
    {
        Step(new Vector2(300, 100), true);
        Step(new Vector2(300, 100), false);

        Assert.That(UiColorDrop.Take(_blue, out _), Is.False);
    }

    [Test]
    public void LettingGoOverNothingDropsNothing()
    {
        Step(new Vector2(100, 100), true);
        Step(new Vector2(200, 300), true);
        Step(new Vector2(200, 300), false);

        Assert.That(UiColorDrop.Take(_blue, out _), Is.False);
        Assert.That(UiColorDrop.Take(_red, out _), Is.False);
    }
}

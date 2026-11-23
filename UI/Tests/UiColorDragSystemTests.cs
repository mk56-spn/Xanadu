using System.Collections.Generic;
using Xanadu.Tests.Support.Fakes;
using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Core.Maths;
using Xanadu.Tests.Support;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Input;
using Xanadu.Ui.Systems.Ui;
using Position = Xanadu.Core.Position;

namespace Xanadu.Ui.Tests;

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

    /// <summary>Every <see cref="ColorDropped"/> heard, in order: which swatch, which colour.</summary>
    private readonly List<(Entity On, Color Color)> _dropped = [];

    protected override void OnEcsSetUp()
    {
        _dropped.Clear();
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
            new FillColor(color));
        swatch.AddTag<UiColorSwatch>();
        swatch.AddTag<UiColorDrop>();
        swatch.OnColorDropped(dropped => _dropped.Add((swatch, dropped)));
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

        Assert.That(_dropped, Is.EqualTo(new[] { (_blue, Colors.Red) }), "one drop, on the swatch let go over");
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

        Assert.That(_dropped, Is.Empty);
    }

    [Test]
    public void LettingGoOverNothingDropsNothing()
    {
        Step(new Vector2(100, 100), true);
        Step(new Vector2(200, 300), true);
        Step(new Vector2(200, 300), false);

        Assert.That(_dropped, Is.Empty);
    }
}

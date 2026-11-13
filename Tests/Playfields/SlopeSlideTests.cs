using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Physics;

namespace Xanadu.Tests.Playfields;

/// <summary>Which ramps a player box is sliding on, and which way is down them.</summary>
[TestFixture]
public sealed class SlopeSlideTests
{
    private static readonly PlayfieldView View = new(Vector2.Zero, 1f, new Vector2(1_000, 1_000));
    private static readonly Vector2 Size = new(30, 100);

    /// <summary>A 200 by 200 ramp at (400, 600), rising to the right: from (400, 800) up to (600, 600).</summary>
    private static EntityStore Ramp(BlockSlope slope)
    {
        var store = new EntityStore();
        store.CreateEntity(new Block(new Vector2(400, 600), new Vector2(200, 200), slope: slope));
        return store;
    }

    [Test]
    public void OnARampRisingRightTheWayDownIsLeft()
    {
        // The box's bottom-right corner on the surface at x 515, y 685.
        Vector2 centre = new(500, 685 - Size.Y / 2);

        bool on = SlopeSlideSystem.TryDownhill(Ramp(BlockSlope.UpRight), View, centre, Size, Vector2.Zero, out Vector2 downhill);

        Assert.That(on, Is.True);
        Assert.That(downhill.X, Is.LessThan(0));
        Assert.That(downhill.Y, Is.GreaterThan(0));
    }

    [Test]
    public void OnARampRisingLeftTheWayDownIsRight()
    {
        // Rising left: from (400, 600) down to (600, 800); bottom-left corner at x 485, y 685.
        Vector2 centre = new(500, 685 - Size.Y / 2);

        bool on = SlopeSlideSystem.TryDownhill(Ramp(BlockSlope.UpLeft), View, centre, Size, Vector2.Zero, out Vector2 downhill);

        Assert.That(on, Is.True);
        Assert.That(downhill.X, Is.GreaterThan(0));
    }

    [Test]
    public void AboveTheRampIsNotOnIt()
    {
        Vector2 centre = new(500, 500);

        Assert.That(SlopeSlideSystem.TryDownhill(Ramp(BlockSlope.UpRight), View, centre, Size, Vector2.Zero, out _), Is.False);
    }

    [Test]
    public void LeavingTheSurfaceIsNotSliding()
    {
        Vector2 centre = new(500, 685 - Size.Y / 2);

        Assert.That(SlopeSlideSystem.TryDownhill(Ramp(BlockSlope.UpRight), View, centre, Size, new Vector2(0, -5), out _), Is.False);
    }

    [Test]
    public void APlainBlockIsNoRamp()
    {
        Vector2 centre = new(500, 600 - Size.Y / 2);

        Assert.That(SlopeSlideSystem.TryDownhill(Ramp(BlockSlope.Off), View, centre, Size, Vector2.Zero, out _), Is.False);
    }
}

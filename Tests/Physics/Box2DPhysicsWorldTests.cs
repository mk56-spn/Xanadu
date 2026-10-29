using System.Linq;
using Box2DSharp.Collision.Shapes;
using Box2DSharp.Dynamics;
using Godot;
using NUnit.Framework;
using Xanadu.Systems.Physics;
using NVector2 = System.Numerics.Vector2;

namespace Xanadu.Tests.Physics;

[TestFixture]
public sealed class Box2DPhysicsWorldTests
{
    [Test]
    public void CharacterWithMomentumFallsAndRestsOnGround()
    {
        using var physics = new Box2DPhysicsWorld();
        physics.CreateStaticBox(new Vector2(640, 680), new Vector2(1200, 40));

        Body body = physics.CreateDynamicBox(
            new Vector2(200, 400),
            new Vector2(30, 100),
            mass: 1f,
            friction: 0.15f);

        body.ApplyLinearImpulseToCenter(new NVector2(3f, -4f), true);

        for (int i = 0; i < 240; i++)
            physics.Step(Box2DPhysicsWorld.FixedTimeStep);

        Vector2 position = Box2DPhysicsWorld.ToScreen(body.GetPosition());

        Assert.That(position.X, Is.GreaterThan(250));
        Assert.That(position.Y, Is.InRange(580f, 630f));
        Assert.That(body.LinearVelocity.Y, Is.LessThan(1f));
    }

    [Test]
    public void GravityIsLockedAtFifteen()
    {
        using var physics = new Box2DPhysicsWorld();

        Assert.That(Box2DPhysicsWorld.DefaultGravity, Is.EqualTo(15f).Within(0.01f));
        Assert.That(physics.Gravity, Is.EqualTo(15f).Within(0.01f));
    }

    [Test]
    public void DynamicBoxHasCharacterMassAndDimensions()
    {
        using var physics = new Box2DPhysicsWorld();
        Body body = physics.CreateDynamicBox(
            new Vector2(200, 300),
            new Vector2(30, 100),
            mass: 1f);

        Assert.That(body.Mass, Is.EqualTo(1f).Within(0.01f));

        var shape = (PolygonShape)body.FixtureList[0].Shape;
        float width = (shape.Vertices.Max(v => v.X) - shape.Vertices.Min(v => v.X)) *
                      Box2DPhysicsWorld.PixelsPerMeter;
        float height = (shape.Vertices.Max(v => v.Y) - shape.Vertices.Min(v => v.Y)) *
                       Box2DPhysicsWorld.PixelsPerMeter;

        Assert.That(width, Is.EqualTo(30f).Within(0.1f));
        Assert.That(height, Is.EqualTo(100f).Within(0.1f));
    }
}

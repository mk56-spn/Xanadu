using System;
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
    public void ABodyRestingOnABlockHoldsStillAtTheFixedStep()
    {
        using var physics = new Box2DPhysicsWorld();
        physics.CreateStaticBox(new Vector2(640, 680), new Vector2(1200, 40));
        Body body = physics.CreateDynamicBox(new Vector2(640, 600), new Vector2(30, 100));

        for (int i = 0; i < 480; i++)
            physics.Step(Box2DPhysicsWorld.FixedTimeStep);

        float settled = body.GetPosition().Y;
        for (int i = 0; i < 480; i++)
        {
            physics.Step(Box2DPhysicsWorld.FixedTimeStep);
            Assert.That(
                MathF.Abs(body.GetPosition().Y - settled) * Box2DPhysicsWorld.PixelsPerMeter,
                Is.LessThan(0.1f));
            Assert.That(
                MathF.Abs(body.LinearVelocity.Y) * Box2DPhysicsWorld.PixelsPerMeter,
                Is.LessThan(5f));
        }
    }

    [Test]
    public void AMomentumLaunchReachesItsAuthoredHeightSooner()
    {
        using var physics = new Box2DPhysicsWorld();
        Body body = physics.CreateDynamicBox(new Vector2(640, 2000), new Vector2(30, 100));
        var note = new Xanadu.Ecs.Note(0, Vector2.Zero)
        {
            Kind = Xanadu.Ecs.NoteKind.Momentum,
            Momentum = 600f,
        };
        body.SetLinearVelocity(
            Xanadu.Systems.Playfields.PlayerMovementSystem.MomentumVelocity(note, Xanadu.Ecs.DashDirection.Up));

        float start = body.GetPosition().Y;
        float peak = start;
        int peakStep = 0;
        for (int i = 1; i <= 480; i++)
        {
            physics.Step(Box2DPhysicsWorld.FixedTimeStep);
            if (body.GetPosition().Y < peak)
            {
                peak = body.GetPosition().Y;
                peakStep = i;
            }
        }

        float height = (start - peak) * Box2DPhysicsWorld.PixelsPerMeter;
        float seconds = peakStep * Box2DPhysicsWorld.FixedTimeStep;
        TestContext.Out.WriteLine($"peak {height} px in {seconds} s");

        // 600 px/s against the reference gravity of 15 peaks at 120 px in 0.4 s.
        Assert.That(height, Is.EqualTo(120f).Within(3f));
        Assert.That(seconds, Is.LessThan(0.4f));
    }

    [Test]
    public void GravityIsLockedAtItsTunedValue()
    {
        using var physics = new Box2DPhysicsWorld();

        Assert.That(Box2DPhysicsWorld.DefaultGravity, Is.EqualTo(22.2f).Within(0.01f));
        Assert.That(physics.Gravity, Is.EqualTo(22.2f).Within(0.01f));
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

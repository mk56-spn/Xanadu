using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Editor.Analysis;
using Xanadu.Systems.Physics;
using Xanadu.Systems.Playfields;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.Playfields;

[TestFixture]
public sealed class PlayerSpeedCapTests
{
    private static float Max => Box2DPhysicsWorld.MaxSpeed;

    [Test]
    public void TheCapIsWhatTheStrongestMomentumNoteGives()
    {
        var note = new Note(
            0,
            Vector2.Zero,
            kind: NoteKind.Momentum,
            momentum: Box2DPhysicsWorld.MaxMomentum);

        Assert.That(
            PlayerMovementSystem.MomentumSpeed(note),
            Is.EqualTo(Max).Within(0.0001f));
    }

    [Test]
    public void SidewaysSpeedIsCappedBothWays()
    {
        Assert.That(PlayerSpeedCapSystem.Capped(new Vector2(Max * 2, 0)).X, Is.EqualTo(Max));
        Assert.That(PlayerSpeedCapSystem.Capped(new Vector2(-Max * 2, 0)).X, Is.EqualTo(-Max));
    }

    [Test]
    public void OnlyFallingIsCappedVertically()
    {
        Assert.That(PlayerSpeedCapSystem.Capped(new Vector2(0, Max * 2)).Y, Is.EqualTo(Max));
        Assert.That(PlayerSpeedCapSystem.Capped(new Vector2(0, -Max * 2)).Y, Is.EqualTo(-Max * 2));
    }

    [Test]
    public void AFallNeverPassesTheCap()
    {
        using var physics = new Box2DPhysicsWorld();
        var store = new EntityStore();
        var root = new SystemRoot(store);
        root.Add(new PlayerSpeedCapSystem());
        Box2DSharp.Dynamics.Body body =
            physics.CreateDynamicBox(new Vector2(500, 0), new Vector2(30, 100));
        store.CreateEntity(
            new Player(new Vector2(30, 100)),
            new Position(new Vector2(500, 0)),
            new PhysicsBody(body));

        for (int i = 0; i < 180; i++)
        {
            root.Update(new UpdateTick(1f / 60f, 0));
            physics.Step(1f / 60f);
        }

        root.Update(new UpdateTick(1f / 60f, 0));
        Assert.That(body.LinearVelocity.Y, Is.EqualTo(Max).Within(0.001f));
    }

    [Test]
    public void TheAnalyzerFallsAtTerminalSpeedOnceItReachesIt()
    {
        float gravity = Box2DPhysicsWorld.DefaultGravity * Box2DPhysicsWorld.PixelsPerMeter;
        float terminal = Max * Box2DPhysicsWorld.PixelsPerMeter;

        Assert.That(
            PlayabilityAnalyzer.FallDistance(gravity, 0.1f),
            Is.EqualTo(0.5f * gravity * 0.01f).Within(0.01f));
        Assert.That(
            PlayabilityAnalyzer.FallDistance(gravity, 2f) -
            PlayabilityAnalyzer.FallDistance(gravity, 1f),
            Is.EqualTo(terminal).Within(0.01f));
    }
}

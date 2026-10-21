using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Systems.Physics;
using NVector2 = System.Numerics.Vector2;
using Position = Writer.Ecs.Position;

namespace Writer.Tests.Physics;

[TestFixture]
public sealed class PhysicsEcsTests
{
    [Test]
    public void MomentumIsAppliedToCharacterAndRemoved()
    {
        using var physics = new Box2DPhysicsWorld();
        var store = new EntityStore();
        var root = new SystemRoot(store);
        root.Add(new CharacterMomentumSystem());
        root.Add(new PhysicsStepSystem(physics));

        var body = physics.CreateDynamicBox(new Vector2(200, 300), new Vector2(30, 100));
        Entity character = store.CreateEntity(
            new Position(new Vector2(200, 300)),
            new Character(1f),
            new PhysicsBody(body),
            new Momentum(new NVector2(2f, 0f)),
            Tags.Get<ApplyMomentum>());

        root.Update(new UpdateTick(Box2DPhysicsWorld.FixedTimeStep, 0));

        Assert.That(character.Tags.Has<ApplyMomentum>(), Is.False);
        Assert.That(body.LinearVelocity.X, Is.GreaterThan(1f));
        Assert.That(character.GetComponent<Position>().Value.X, Is.GreaterThan(200f));
        Assert.That(character.GetComponent<Momentum>().Value.X, Is.GreaterThan(0f));
    }
}

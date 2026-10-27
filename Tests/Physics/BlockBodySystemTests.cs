using Box2DSharp.Dynamics;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Physics;

namespace Xanadu.Tests.Physics;

[TestFixture]
public sealed class BlockBodySystemTests
{
    [Test]
    public void EditorBlocksGetNoBody()
    {
        var store = new EntityStore();
        CreateView(store, PlayfieldKind.Editor, Vector2.Zero, 1f);
        Entity block = store.CreateEntity(
            new Block(new Vector2(100, 100), new Vector2(200, 100)));

        using var physics = new Box2DPhysicsWorld();
        var root = new SystemRoot(store);
        root.Add(new BlockBodySystem(physics, store));
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(block.HasComponent<PhysicsBody>(), Is.False);
    }

    [Test]
    public void PlayBlocksBecomeStaticBodiesInScreenSpace()
    {
        var store = new EntityStore();
        PlayfieldView view = CreateView(
            store,
            PlayfieldKind.Play,
            new Vector2(100, 50),
            0.5f);
        Entity block = store.CreateEntity(
            new Block(new Vector2(400, 200), new Vector2(200, 100)));

        using var physics = new Box2DPhysicsWorld();
        var root = new SystemRoot(store);
        root.Add(new BlockBodySystem(physics, store));
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(block.HasComponent<PhysicsBody>(), Is.True);
        Body body = block.GetComponent<PhysicsBody>().Body;
        Assert.That(body.BodyType, Is.EqualTo(BodyType.StaticBody));

        Vector2 expected = view.PlayfieldToScreen(new Vector2(500, 250));
        Vector2 actual = Box2DPhysicsWorld.ToScreen(body.GetPosition());
        Assert.That(actual.X, Is.EqualTo(expected.X).Within(0.05f));
        Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(0.05f));
    }

    [Test]
    public void PlayBlocksOnlyGetOneBody()
    {
        var store = new EntityStore();
        CreateView(store, PlayfieldKind.Play, Vector2.Zero, 1f);
        Entity block = store.CreateEntity(
            new Block(new Vector2(100, 100), new Vector2(200, 100)));

        using var physics = new Box2DPhysicsWorld();
        var root = new SystemRoot(store);
        root.Add(new BlockBodySystem(physics, store));
        root.Update(new UpdateTick(1f / 60f, 0));
        Body first = block.GetComponent<PhysicsBody>().Body;
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(ReferenceEquals(first, block.GetComponent<PhysicsBody>().Body), Is.True);
        Assert.That(physics.World.BodyCount, Is.EqualTo(1));
    }

    [Test]
    public void APlayBlockStopsAFallingCharacter()
    {
        var store = new EntityStore();
        CreateView(store, PlayfieldKind.Play, Vector2.Zero, 1f);
        store.CreateEntity(
            new Block(new Vector2(300, 580), new Vector2(400, 40)));

        using var physics = new Box2DPhysicsWorld();
        var root = new SystemRoot(store);
        root.Add(new BlockBodySystem(physics, store));
        root.Update(new UpdateTick(1f / 60f, 0));

        Body character = physics.CreateDynamicBox(
            new Vector2(500, 300),
            new Vector2(30, 100));

        for (int i = 0; i < 240; i++)
            physics.Step(Box2DPhysicsWorld.FixedTimeStep);

        Vector2 position = Box2DPhysicsWorld.ToScreen(character.GetPosition());
        Assert.That(position.Y, Is.InRange(500f, 560f));
    }

    private static PlayfieldView CreateView(
        EntityStore store,
        PlayfieldKind kind,
        Vector2 origin,
        float scale)
    {
        var view = new PlayfieldView(origin, scale, new Vector2(1000, 1000));
        store.CreateEntity(new PlayfieldMode(kind), view);
        return view;
    }
}

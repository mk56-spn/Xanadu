using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Physics;
using Xanadu.Systems.Playfields;
using Xanadu.Tests.Support;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.Playfields;

/// <summary>Where a player is drawn between physics steps, which are slower than frames.</summary>
[TestFixture]
public sealed class PlayerSmoothingTests : EcsSystemTest
{
    private const float Frame = Box2DPhysicsWorld.FixedTimeStep / 4f;

    private Box2DPhysicsWorld _physics = null!;

    protected override void OnEcsSetUp()
    {
        _physics = new Box2DPhysicsWorld();
        Root.Add(new PhysicsStepSystem(_physics));
        Root.Add(new PlayerSmoothingSystem());
    }

    [TearDown]
    public void DisposePhysics() => _physics.Dispose();

    private Entity Player(Vector2 at)
    {
        var body = _physics.CreateDynamicBox(at, new Vector2(30, 100));
        return Store.CreateEntity(
            new Player(new Vector2(30, 100)),
            new Position(at),
            new Character(1f),
            new Momentum(),
            new PhysicsBody(body),
            new PlayerSmoothing(at));
    }

    private void RunFrame() => Root.Update(new UpdateTick(Frame, 0));

    private static Vector2 Drawn(Entity player) => PlayerSmoothing.DrawnPosition(player);

    [Test]
    public void AFallingPlayerIsDrawnFurtherDownEveryFrameBetweenSteps()
    {
        Entity player = Player(new Vector2(400, 100));
        for (int i = 0; i < 40; i++)
            RunFrame();

        int frozen = 0;
        float last = Drawn(player).Y;
        for (int i = 0; i < 40; i++)
        {
            RunFrame();
            float y = Drawn(player).Y;
            Assert.That(y, Is.GreaterThanOrEqualTo(last), "the drawn position never goes back up");
            if (y == last)
                frozen++;
            last = y;
        }

        Assert.That(frozen, Is.LessThanOrEqualTo(1), "it moves on the frames between steps too");
    }

    [Test]
    public void TheDrawnPositionStaysWithinTheLastStep()
    {
        Entity player = Player(new Vector2(400, 100));
        for (int i = 0; i < 60; i++)
        {
            RunFrame();
            float logical = player.GetComponent<Position>().Value.Y;
            Assert.That(Drawn(player).Y, Is.LessThanOrEqualTo(logical + 0.01f));
            Assert.That(logical - Drawn(player).Y, Is.LessThan(20f));
        }
    }

    [Test]
    public void AJumpInPositionIsDrawnStraightAway()
    {
        Entity player = Player(new Vector2(400, 100));
        for (int i = 0; i < 20; i++)
            RunFrame();

        // The way a dash or the arena moves the player, between frames.
        var target = new Vector2(900, 50);
        player.GetComponent<Position>().Value = target;
        player.GetComponent<PhysicsBody>().Body.SetTransform(Box2DPhysicsWorld.ToPhysics(target), 0f);
        Root.Update(new UpdateTick(0f, 0));

        Assert.That(Drawn(player).DistanceTo(target), Is.LessThan(1f));
    }

    [Test]
    public void APlayerWithoutABodyIsDrawnWhereItStands()
    {
        Entity player = Store.CreateEntity(new Position(new Vector2(12, 34)));

        Assert.That(PlayerSmoothing.DrawnPosition(player), Is.EqualTo(new Vector2(12, 34)));
    }

    [Test]
    public void TheSubpixelUndoesTheLayersRounding()
    {
        var camera = new PlayCamera(new Rect2(0, 0, 100, 100)) { Offset = new Vector2(10.4f, 20.6f) };

        Vector2 onScreen = new Vector2(50, 50) + camera.Subpixel - camera.LayerShift;

        Assert.That(onScreen.DistanceTo(new Vector2(50, 50) - camera.Shift), Is.LessThan(0.001f));
    }
}

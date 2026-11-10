using Box2DSharp.Dynamics;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Physics;

namespace Xanadu.Tests.Physics;

[TestFixture]
public sealed class OneWayBlockTests
{
    private static readonly Vector2 BlockCenter = new(500, 500);
    private static readonly Vector2 BlockSize = new(400, 40);
    private static readonly Vector2 PlayerSize = new(40, 80);

    /// <summary>Where the player's centre sits when standing on the block's top.</summary>
    private const float RestingY = 500 - 20 - 40;

    private static float Run(Box2DPhysicsWorld physics, Body player, float seconds)
    {
        float top = float.MaxValue;
        for (float time = 0; time < seconds; time += Box2DPhysicsWorld.FixedTimeStep)
        {
            physics.Step(Box2DPhysicsWorld.FixedTimeStep);
            top = System.MathF.Min(top, Box2DPhysicsWorld.ToScreen(player.GetPosition()).Y);
        }

        return top;
    }

    private static Body Player(Box2DPhysicsWorld physics, Vector2 center, Vector2 velocity)
    {
        Body body = physics.CreateDynamicBox(center, PlayerSize);
        body.SetLinearVelocity(velocity);
        return body;
    }

    private static float Y(Body body) => Box2DPhysicsWorld.ToScreen(body.GetPosition()).Y;

    private static float X(Body body) => Box2DPhysicsWorld.ToScreen(body.GetPosition()).X;

    [Test]
    public void FallingPlayerLandsOnAnUpBlock()
    {
        using var physics = new Box2DPhysicsWorld();
        physics.CreateStaticBox(BlockCenter, BlockSize, oneWay: BlockOneWay.Up);
        Body player = Player(physics, new Vector2(500, 300), Vector2.Zero);

        Run(physics, player, 1f);

        Assert.That(Y(player), Is.EqualTo(RestingY).Within(2f));
    }

    [Test]
    public void VeryFastFallStillLandsOnAnUpBlock()
    {
        using var physics = new Box2DPhysicsWorld();
        physics.CreateStaticBox(BlockCenter, BlockSize, oneWay: BlockOneWay.Up);
        Body player = Player(physics, new Vector2(500, 200), new Vector2(0, 100f));

        Run(physics, player, 1f);

        Assert.That(Y(player), Is.EqualTo(RestingY).Within(2f));
    }

    [Test]
    public void RisingPlayerPassesUpThroughAnUpBlockAndLandsOnIt()
    {
        using var physics = new Box2DPhysicsWorld();
        physics.CreateStaticBox(BlockCenter, BlockSize, oneWay: BlockOneWay.Up);
        Body player = Player(physics, new Vector2(500, 620), new Vector2(0, -11f));

        float top = Run(physics, player, 3f);

        Assert.That(top, Is.LessThan(RestingY));
        Assert.That(Y(player), Is.EqualTo(RestingY).Within(2f));
    }

    [Test]
    public void SolidBlockStillStopsARisingPlayer()
    {
        using var physics = new Box2DPhysicsWorld();
        physics.CreateStaticBox(BlockCenter, BlockSize);
        Body player = Player(physics, new Vector2(500, 620), new Vector2(0, -11f));

        float top = Run(physics, player, 0.5f);

        Assert.That(top, Is.GreaterThan(BlockCenter.Y));
    }

    [Test]
    public void PlayerWalksThroughTheSideOfAnUpBlock()
    {
        using var physics = new Box2DPhysicsWorld();
        physics.World.Gravity = Vector2.Zero;
        physics.CreateStaticBox(BlockCenter, BlockSize, oneWay: BlockOneWay.Up);
        Body player = Player(physics, new Vector2(200, 500), new Vector2(10f, 0));

        Run(physics, player, 1f);

        Assert.That(X(player), Is.GreaterThan(800f));
    }

    [Test]
    public void DashEndingInsideAnUpBlockIsNotPushedOutOfIt()
    {
        using var physics = new Box2DPhysicsWorld();
        physics.CreateStaticBox(BlockCenter, BlockSize, oneWay: BlockOneWay.Up);
        Body player = Player(physics, new Vector2(500, 500), Vector2.Zero);

        Run(physics, player, 0.5f);

        Assert.That(Y(player), Is.GreaterThan(500f));
    }

    /// <summary>x and y point from the colliding side into the block.</summary>
    [TestCase(BlockOneWay.Up, 0f, 1f)]
    [TestCase(BlockOneWay.Right, -1f, 0f)]
    [TestCase(BlockOneWay.Down, 0f, -1f)]
    [TestCase(BlockOneWay.Left, 1f, 0f)]
    public void EachSideStopsOnlyWhatComesAtIt(BlockOneWay side, float x, float y)
    {
        var inward = new Vector2(x, y);

        Assert.That(Travel(side, inward), Is.LessThan(-60f), "from the colliding side");
        Assert.That(Travel(side, -inward), Is.GreaterThan(200f), "from the far side");
    }

    /// <summary>How far past the block's centre a box gets flying through it along a direction.</summary>
    private static float Travel(BlockOneWay side, Vector2 direction)
    {
        using var physics = new Box2DPhysicsWorld();
        physics.World.Gravity = Vector2.Zero;
        physics.CreateStaticBox(BlockCenter, new Vector2(100, 100), oneWay: side);
        Body player = physics.CreateDynamicBox(BlockCenter - direction * 200f, new Vector2(40, 40));
        player.SetLinearVelocity(new Vector2(direction.X, direction.Y) * 8f);

        Run(physics, player, 1f);

        return (Box2DPhysicsWorld.ToScreen(player.GetPosition()) - BlockCenter).Dot(direction);
    }
}

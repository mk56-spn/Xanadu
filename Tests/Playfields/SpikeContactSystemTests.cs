using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Playfields;
using Xanadu.Tests.Support;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.Playfields;

[TestFixture]
public sealed class SpikeContactSystemTests : EcsSystemTest
{
    [Test]
    public void TouchingASpikeKillsThePlayer()
    {
        Entity playfield = CreatePlayfield(new Vector2(450, 450));
        Store.CreateEntity(
            new Spike(new Vector2(400, 400), new Vector2(100, 100)));
        Root.Add(new SpikeContactSystem(Store));

        Frame();

        Assert.That(playfield.GetComponent<PlaySession>().Died, Is.True);
    }

    [Test]
    public void StandingClearOfASpikeIsSafe()
    {
        Entity playfield = CreatePlayfield(new Vector2(450, 450));
        Store.CreateEntity(
            new Spike(new Vector2(600, 400), new Vector2(100, 100)));
        Root.Add(new SpikeContactSystem(Store));

        Frame();

        Assert.That(playfield.GetComponent<PlaySession>().Died, Is.False);
    }

    [TestCase(415, 370, TestName = "In the empty top corner of the tile")]
    [TestCase(450, 395, TestName = "Just above the hitbox tip, under the drawn tip")]
    public void TheTileAroundTheHitboxIsSafe(float x, float y)
    {
        Entity playfield = CreatePlayfield(new Vector2(x, y));
        playfield.GetComponent<Player>().Size = new Vector2(10, 20);
        Store.CreateEntity(
            new Spike(new Vector2(400, 400), new Vector2(100, 100)));
        Root.Add(new SpikeContactSystem(Store));

        Frame();

        Assert.That(playfield.GetComponent<PlaySession>().Died, Is.False);
    }

    [Test]
    public void TheHitboxTurnsWithTheSpike()
    {
        // Under a spike pointing down, the top of the tile is deadly and the
        // bottom corners are safe.
        Entity safe = CreatePlayfield(new Vector2(415, 485));
        safe.GetComponent<Player>().Size = new Vector2(10, 10);
        Store.CreateEntity(
            new Spike(new Vector2(400, 400), new Vector2(100, 100), 180f));
        Root.Add(new SpikeContactSystem(Store));

        Frame();
        Assert.That(safe.GetComponent<PlaySession>().Died, Is.False);

        safe.GetComponent<Position>().Value = new Vector2(415, 415);
        Frame();
        Assert.That(safe.GetComponent<PlaySession>().Died, Is.True);
    }

    [Test]
    public void SpikesOnlyBiteOncePlayHasStarted()
    {
        Entity playfield = CreatePlayfield(new Vector2(450, 450));
        playfield.GetComponent<PlaySession>().Started = false;
        Store.CreateEntity(
            new Spike(new Vector2(400, 400), new Vector2(100, 100)));
        Root.Add(new SpikeContactSystem(Store));

        Frame();

        Assert.That(playfield.GetComponent<PlaySession>().Died, Is.False);
    }

    [Test]
    public void APausedPlayfieldIgnoresSpikes()
    {
        Entity playfield = CreatePlayfield(new Vector2(450, 450));
        playfield.AddTag<PlayPaused>();
        Store.CreateEntity(
            new Spike(new Vector2(400, 400), new Vector2(100, 100)));
        Root.Add(new SpikeContactSystem(Store));

        Frame();

        Assert.That(playfield.GetComponent<PlaySession>().Died, Is.False);
    }

    private Entity CreatePlayfield(Vector2 playerPosition)
    {
        Entity status = Store.CreateEntity(new Text(""));
        Entity playfield = Store.CreateEntity(
            new Player(new Vector2(30, 200)),
            new Position(playerPosition),
            new PlayfieldView(Vector2.Zero, 1f, new Vector2(1000, 1000)),
            new PlaySession(status));
        playfield.GetComponent<PlaySession>().Started = true;
        return playfield;
    }
}

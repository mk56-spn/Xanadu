using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Editor;
using Xanadu.Systems.Physics;
using Xanadu.Systems.Playfields;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.Editor;

/// <summary>Recording the player's hitbox in a preview and reading it back by song time.</summary>
[TestFixture]
public sealed class PlayerPathTests
{
    private EntityStore _store = null!;
    private SystemRoot _root = null!;
    private Box2DPhysicsWorld _physics = null!;
    private Entity _player;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _root = new SystemRoot(_store);
        _physics = new Box2DPhysicsWorld();
        _root.Add(new PlayerPathRecordSystem());
        _player = _store.CreateEntity(
            new PlayerPathRecorder(),
            new Position(new Vector2(110, 120)),
            new PlayfieldView(new Vector2(10, 20), 2f, new Vector2(1000, 1000)),
            new Playhead(0, 120, 0, true),
            new PlaySession { Started = true },
            new PhysicsBody(_physics.CreateDynamicBox(new Vector2(110, 120), new Vector2(30, 100))));
    }

    [TearDown]
    public void TearDown() => _physics.Dispose();

    private void Frame(long timeMs, Vector2 at, float speed = 0f)
    {
        _player.GetComponent<Playhead>().TimeMs = timeMs;
        _player.GetComponent<Position>().Value = at;
        _player.GetComponent<PhysicsBody>().Body.SetLinearVelocity(
            new System.Numerics.Vector2(speed, 0f));
        _root.Update(new UpdateTick(1f / 60f, 0));
    }

    private List<PlayerPathSample> Samples => _player.GetComponent<PlayerPathRecorder>().Samples;

    [Test]
    public void TheFrameANoteIsHitIsMarkedEvenWithoutMoving()
    {
        _player.AddComponent(new ScoreState());
        Frame(0, new Vector2(110, 120));

        _player.GetComponent<ScoreState>().NotesHit = 1;
        Frame(10, new Vector2(110, 120));
        Frame(20, new Vector2(150, 120), speed: 100f);

        Assert.That(Samples, Has.Count.EqualTo(3));
        Assert.That(Samples[0].Hit, Is.False);
        Assert.That(Samples[1].Hit, Is.True);
        Assert.That(Samples[1].TimeMs, Is.EqualTo(10));
        Assert.That(Samples[2].Hit, Is.False);
    }

    [Test]
    public void SamplesAreInFieldUnits()
    {
        Frame(0, new Vector2(110, 120));

        Assert.That(Samples[0].Position, Is.EqualTo(new Vector2(50, 50)));
    }

    [Test]
    public void NothingIsRecordedBeforeTheSongStartsOrWhilePaused()
    {
        _player.GetComponent<PlaySession>().Started = false;
        Frame(0, new Vector2(110, 120));
        _player.GetComponent<PlaySession>().Started = true;
        _player.AddTag<PlayPaused>();
        Frame(500, new Vector2(110, 120));

        Assert.That(Samples, Is.Empty);
    }

    [Test]
    public void SmallMovesWaitButTimeStillPasses()
    {
        Frame(0, new Vector2(110, 120));
        Frame(16, new Vector2(112, 120), speed: 1f);
        Assert.That(Samples, Has.Count.EqualTo(1));

        Frame(PlayerPathRecordSystem.MaxGapMs, new Vector2(112, 120));
        Assert.That(Samples, Has.Count.EqualTo(2));
    }

    [Test]
    public void ATeleportIsMarkedAsAJumpAndSteadyMovementIsNot()
    {
        Frame(0, new Vector2(110, 120));
        Frame(16, new Vector2(130, 120), speed: 12f);
        Frame(32, new Vector2(800, 120), speed: 0f);

        Assert.That(Samples[1].Jump, Is.False);
        Assert.That(Samples[2].Jump, Is.True);
    }

    private static readonly List<PlayerPathSample> Path = new()
    {
        new(new Vector2(0, 0), 100, false),
        new(new Vector2(10, 0), 200, false),
        new(new Vector2(500, 0), 300, true),
    };

    [TestCase(99)]
    [TestCase(301)]
    public void NoPositionOutsideTheRecording(long time) =>
        Assert.That(EditorPlayfieldDrawSystem.PathPositionAt(Path, time), Is.Null);

    [Test]
    public void ThePositionEasesBetweenSamples() =>
        Assert.That(EditorPlayfieldDrawSystem.PathPositionAt(Path, 150), Is.EqualTo(new Vector2(5, 0)));

    [Test]
    public void AJumpHoldsUntilItHappens()
    {
        Assert.That(EditorPlayfieldDrawSystem.PathPositionAt(Path, 299), Is.EqualTo(new Vector2(10, 0)));
        Assert.That(EditorPlayfieldDrawSystem.PathPositionAt(Path, 300), Is.EqualTo(new Vector2(500, 0)));
    }
}

using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Writer.Animation;
using Writer.Ecs;
using Writer.Systems.Characters;
using Writer.Systems.Physics;
using Writer.Tests.Support;
using Position = Writer.Ecs.Position;

namespace Writer.Tests.Characters;

[TestFixture]
public sealed class CharacterMotionTests : EcsSystemTest
{
    // A 1000 x 500 playfield at the screen origin; a 100 px tall player rests at y = 450.
    private const float Floor = 450f;

    private Box2DPhysicsWorld _physics = null!;

    protected override void OnEcsSetUp()
    {
        _physics = new Box2DPhysicsWorld();
        Root.Add(new CharacterMotionSystem());
        Root.Add(new CharacterRigAnimateSystem());
    }

    [TearDown]
    public void DisposePhysics() => _physics.Dispose();

    private Entity Player(float y, bool falling = true, bool simulated = true)
    {
        XafPackage package = Package(falling);
        var runtime = new AnimatorRuntime(package);
        runtime.Play("idle", 0f);
        Entity player = Store.CreateEntity(
            new Player(new Vector2(30, 100)),
            new Position(new Vector2(400, y)),
            new PlayfieldView(Vector2.Zero, 1f, new Vector2(1000, 500)),
            new Playfield(1000, 500),
            new CharacterRig(runtime));
        if (simulated)
            player.AddComponent(new PhysicsBody(_physics.CreateDynamicBox(new Vector2(400, y), new Vector2(30, 100))));
        return player;
    }

    private static void MoveTo(Entity player, float y) =>
        player.GetComponent<Position>().Value = new Vector2(400, y);

    private static CharacterMotionState State(Entity player) =>
        player.GetComponent<CharacterMotion>().State;

    /// <summary>The clip playing, or the one being faded to.</summary>
    private static string Clip(Entity player)
    {
        AnimatorRuntime runtime = player.GetComponent<CharacterRig>().Runtime;
        return runtime.IsCrossfading ? runtime.NextClip : runtime.CurrentClip;
    }

    [Test]
    public void APlayerOnTheFloorIsGroundedAndIdle()
    {
        Entity player = Player(Floor);
        Frame(3);

        Assert.That(State(player), Is.EqualTo(CharacterMotionState.Grounded));
        Assert.That(Clip(player), Is.EqualTo("idle"));
    }

    [Test]
    public void DashingUpPlaysTheJumpClip()
    {
        Entity player = Player(Floor);
        Frame();
        MoveTo(player, Floor - 150);
        Frame();

        Assert.That(State(player), Is.EqualTo(CharacterMotionState.Jumping));
        Assert.That(Clip(player), Is.EqualTo("jumping"));
    }

    [Test]
    public void AJumpTurnsIntoAFallOnceItIsHeldLongEnough()
    {
        Entity player = Player(Floor);
        Frame();
        MoveTo(player, Floor - 150);
        Frame();

        // Gravity pulls the player down a little each frame.
        float y = Floor - 150;
        for (int i = 0; i < 40; i++)
        {
            MoveTo(player, y += 0.5f);
            Frame();
        }

        Assert.That(State(player), Is.EqualTo(CharacterMotionState.Falling));
        Assert.That(Clip(player), Is.EqualTo("falling"));
    }

    [Test]
    public void WithoutAFallingClipTheJumpPoseIsKeptWhileFalling()
    {
        Entity player = Player(Floor - 200, falling: false);
        Frame(2);

        Assert.That(State(player), Is.EqualTo(CharacterMotionState.Falling));
        Assert.That(Clip(player), Is.EqualTo("jumping"));
    }

    private System.Collections.Generic.List<ParticleBurst> Bursts()
    {
        var bursts = new System.Collections.Generic.List<ParticleBurst>();
        foreach (Entity entity in Store.Query<ParticleBurst>().ToEntityList())
            bursts.Add(entity.GetComponent<ParticleBurst>());
        return bursts;
    }

    [Test]
    public void LandingSetsOffDustAtTheFeetOnce()
    {
        Entity player = Player(Floor - 200);
        Frame(2);
        Assert.That(Bursts(), Is.Empty);

        MoveTo(player, Floor);
        Frame();
        Frame(30);

        Assert.That(Bursts(), Has.Count.EqualTo(1));
        Assert.That(Bursts()[0].Kind, Is.EqualTo(ParticleKind.Landing));
        // The player is 100 px tall and its position is the centre.
        Assert.That(Bursts()[0].Position, Is.EqualTo(new Vector2(400, Floor + 50)));
    }

    [Test]
    public void StandingOrJumpingSetsOffNoDust()
    {
        Entity player = Player(Floor);
        Frame(3);
        MoveTo(player, Floor - 150);
        Frame(2);

        Assert.That(Bursts(), Is.Empty);
    }

    [Test]
    public void OnlyTouchingDownFromTheAirCountsAsLanding()
    {
        Assert.That(CharacterMotionSystem.Landed(CharacterMotionState.Falling, CharacterMotionState.Landing), Is.True);
        Assert.That(CharacterMotionSystem.Landed(CharacterMotionState.Jumping, CharacterMotionState.Grounded), Is.True);
        Assert.That(CharacterMotionSystem.Landed(CharacterMotionState.Landing, CharacterMotionState.Grounded), Is.False);
        Assert.That(CharacterMotionSystem.Landed(CharacterMotionState.Grounded, CharacterMotionState.Jumping), Is.False);
        Assert.That(CharacterMotionSystem.Landed(CharacterMotionState.Jumping, CharacterMotionState.Falling), Is.False);
    }

    [Test]
    public void TouchingDownPlaysTheLandingClipThenGoesIdle()
    {
        Entity player = Player(Floor - 200);
        Frame(2);
        MoveTo(player, Floor);
        Frame();

        Assert.That(State(player), Is.EqualTo(CharacterMotionState.Landing));
        Assert.That(Clip(player), Is.EqualTo("landing"));

        // The landing clip is 0.25 s long.
        Frame(30);

        Assert.That(State(player), Is.EqualTo(CharacterMotionState.Grounded));
        Assert.That(Clip(player), Is.EqualTo("idle"));
    }

    [Test]
    public void JumpingAgainMidJumpReplaysTheJump()
    {
        Entity player = Player(Floor);
        Frame();
        MoveTo(player, Floor - 150);
        Frame(10); // past the crossfade, so the jump clip is the current one
        float time = player.GetComponent<CharacterRig>().Runtime.CurrentTime;
        MoveTo(player, Floor - 300);
        Frame();

        Assert.That(State(player), Is.EqualTo(CharacterMotionState.Jumping));
        Assert.That(player.GetComponent<CharacterRig>().Runtime.CurrentTime, Is.LessThan(time));
    }

    [Test]
    public void BeforePlayStartsThePlayerStandsWhereverItIs()
    {
        Entity player = Player(Floor - 200, simulated: false);
        Frame(3);

        Assert.That(State(player), Is.EqualTo(CharacterMotionState.Grounded));
        Assert.That(Clip(player), Is.EqualTo("idle"));
    }

    private static XafPackage Package(bool falling)
    {
        var package = new XafPackage();
        package.Skeleton.Bones.Add(new XafBone { Id = "root", Parent = -1, Rest = Transform2D.Identity });
        package.Clips.Add(new XafClip { Id = "idle", Duration = 2f, Loop = true, LoopEnd = 2f });
        package.Clips.Add(new XafClip { Id = "jumping", Duration = 0.5f });
        package.Clips.Add(new XafClip { Id = "landing", Duration = 0.25f });
        if (falling)
            package.Clips.Add(new XafClip { Id = "falling", Duration = 1f, Loop = true, LoopEnd = 1f });
        return package;
    }
}

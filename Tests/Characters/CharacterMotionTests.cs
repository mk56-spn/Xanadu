using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Systems.Characters;
using Xanadu.Systems.Physics;
using Xanadu.Tests.Support;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.Characters;

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
        runtime.Play(ContactClip.Idle.Id(), 0f);
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

    [Test]
    public void ASlowRiseIsAJumpEvenWhenAFrameMovesLessThanAPixel()
    {
        Entity player = Player(Floor - 150);
        Root.Update(new UpdateTick(1f / 240f, 0));
        player.GetComponent<PhysicsBody>().Body.SetLinearVelocity(new System.Numerics.Vector2(0, -1f));
        MoveTo(player, Floor - 150.4f);

        Root.Update(new UpdateTick(1f / 240f, 0));

        Assert.That(State(player), Is.EqualTo(CharacterMotionState.Jumping));
    }

    [Test]
    public void ALongRiseLetsTheJumpClipPlayAndANewImpulseRestartsIt()
    {
        Entity player = Player(Floor - 150);
        var body = player.GetComponent<PhysicsBody>().Body;
        Root.Update(new UpdateTick(1f / 240f, 0));

        float y = Floor - 150;
        for (int i = 0; i < 20; i++)
        {
            // Gravity slowly eats the rise, as it does in play.
            body.SetLinearVelocity(new System.Numerics.Vector2(0, -3f + i * 0.01f));
            MoveTo(player, y -= 1.2f);
            Root.Update(new UpdateTick(1f / 240f, 0));
        }

        Assert.That(
            player.GetComponent<CharacterMotion>().StateTime,
            Is.GreaterThan(15f / 240f),
            "the clip is not sent back to its start every frame");

        body.SetLinearVelocity(new System.Numerics.Vector2(0, -6f));
        MoveTo(player, y -= 2.5f);
        Root.Update(new UpdateTick(1f / 240f, 0));

        Assert.That(player.GetComponent<CharacterMotion>().StateTime, Is.Zero);
    }

    private static void MoveTo(Entity player, float y) =>
        player.GetComponent<Position>().Value = new Vector2(400, y);

    private static CharacterMotionState State(Entity player) =>
        player.GetComponent<CharacterMotion>().State;

    /// <summary>The clip playing, or the one being faded to.</summary>
    private static ContactClip? Clip(Entity player)
    {
        AnimatorRuntime runtime = player.GetComponent<CharacterRig>().Runtime;
        return ContactClips.FromId(runtime.IsCrossfading ? runtime.NextClip : runtime.CurrentClip);
    }

    [Test]
    public void APlayerOnTheFloorIsGroundedAndIdle()
    {
        Entity player = Player(Floor);
        Frame(3);

        Assert.That(State(player), Is.EqualTo(CharacterMotionState.Grounded));
        Assert.That(Clip(player), Is.EqualTo(ContactClip.Idle));
    }

    [Test]
    public void DashingUpPlaysTheJumpClip()
    {
        Entity player = Player(Floor);
        Frame();
        MoveTo(player, Floor - 150);
        Frame();

        Assert.That(State(player), Is.EqualTo(CharacterMotionState.Jumping));
        Assert.That(Clip(player), Is.EqualTo(ContactClip.Jumping));
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
        Assert.That(Clip(player), Is.EqualTo(ContactClip.Falling));
    }

    [Test]
    public void WithoutAFallingClipTheJumpPoseIsKeptWhileFalling()
    {
        Entity player = Player(Floor - 200, falling: false);
        Frame(2);

        Assert.That(State(player), Is.EqualTo(CharacterMotionState.Falling));
        Assert.That(Clip(player), Is.EqualTo(ContactClip.Jumping));
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
        Assert.That(Bursts()[0].Particle, Is.EqualTo(GameParticles.Landing));
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
        Assert.That(Clip(player), Is.EqualTo(ContactClip.Landing));

        // The landing clip is 0.25 s long.
        Frame(30);

        Assert.That(State(player), Is.EqualTo(CharacterMotionState.Grounded));
        Assert.That(Clip(player), Is.EqualTo(ContactClip.Idle));
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
    public void LandingOnTopOfABlockPlaysTheLandingClipThenGoesIdle()
    {
        // The block's top is at y = 300, so a 100 px tall player rests at y = 250.
        Store.CreateEntity(new Block(new Vector2(350, 300), new Vector2(100, 50)));
        Entity player = Player(100);
        Frame(2);
        Assert.That(State(player), Is.EqualTo(CharacterMotionState.Falling));

        MoveTo(player, 250);
        Frame();

        Assert.That(State(player), Is.EqualTo(CharacterMotionState.Landing));

        Frame(30);

        Assert.That(State(player), Is.EqualTo(CharacterMotionState.Grounded));
        Assert.That(Clip(player), Is.EqualTo(ContactClip.Idle));
    }

    [Test]
    public void OnlyAOneWayBlockThatCollidesOnTopCanBeStoodOn()
    {
        Store.CreateEntity(new Block(new Vector2(350, 300), new Vector2(100, 50), oneWay: BlockOneWay.Down));
        Entity player = Player(250);
        Frame(2);
        Assert.That(State(player), Is.EqualTo(CharacterMotionState.Falling));

        Store.CreateEntity(new Block(new Vector2(350, 300), new Vector2(100, 50), oneWay: BlockOneWay.Up));
        Frame();
        Assert.That(State(player), Is.EqualTo(CharacterMotionState.Landing));
    }

    [Test]
    public void HoveringBesideOrAboveABlockIsNotGrounded()
    {
        // Same height as the block's top, but the player at x = 400 is clear of it.
        Store.CreateEntity(new Block(new Vector2(600, 300), new Vector2(100, 50)));
        Entity beside = Player(250);
        Frame(2);
        Assert.That(State(beside), Is.EqualTo(CharacterMotionState.Falling));

        // Over the block but well above its top.
        Store.CreateEntity(new Block(new Vector2(350, 300), new Vector2(100, 50)));
        Entity above = Player(150);
        Frame(2);
        Assert.That(State(above), Is.EqualTo(CharacterMotionState.Falling));
    }

    [Test]
    public void BeforePlayStartsThePlayerStandsWhereverItIs()
    {
        Entity player = Player(Floor - 200, simulated: false);
        Frame(3);

        Assert.That(State(player), Is.EqualTo(CharacterMotionState.Grounded));
        Assert.That(Clip(player), Is.EqualTo(ContactClip.Idle));
    }

    [Test]
    public void TheGameAsksForEveryHandOverItsStatesCanMake()
    {
        Assert.That(CharacterMotionSystem.Transitions(Package(falling: true)), Is.EquivalentTo(new[]
        {
            (ContactClip.Idle, ContactClip.Jumping), (ContactClip.Landing, ContactClip.Jumping), (ContactClip.Falling, ContactClip.Jumping),
            (ContactClip.Jumping, ContactClip.Falling), (ContactClip.Idle, ContactClip.Falling), (ContactClip.Landing, ContactClip.Falling),
            (ContactClip.Jumping, ContactClip.Landing), (ContactClip.Falling, ContactClip.Landing), (ContactClip.Landing, ContactClip.Idle),
            (ContactClip.Sliding, ContactClip.Jumping), (ContactClip.Sliding, ContactClip.Falling), (ContactClip.Landing, ContactClip.Sliding),
            (ContactClip.Idle, ContactClip.Sliding), (ContactClip.Sliding, ContactClip.Idle),
        }));

        // Without a falling clip the jump pose is kept, so jump-to-fall is no hand-over at all.
        Assert.That(CharacterMotionSystem.Transitions(Package(falling: false)), Is.EquivalentTo(new[]
        {
            (ContactClip.Idle, ContactClip.Jumping), (ContactClip.Landing, ContactClip.Jumping), (ContactClip.Jumping, ContactClip.Landing), (ContactClip.Landing, ContactClip.Idle),
            (ContactClip.Sliding, ContactClip.Jumping), (ContactClip.Landing, ContactClip.Sliding), (ContactClip.Idle, ContactClip.Sliding), (ContactClip.Sliding, ContactClip.Idle),
        }));
    }

    [Test]
    public void AJumpTheRigDoesNotAllowKeepsTheIdleClip()
    {
        Entity player = Player(Floor);
        XafPackage package = player.GetComponent<CharacterRig>().Runtime.Package;
        XafTransitions.Allow(package, ContactClip.Jumping.Id(), ContactClip.Landing.Id());
        Frame();

        MoveTo(player, Floor - 50);
        Frame();

        Assert.That(State(player), Is.EqualTo(CharacterMotionState.Jumping));
        Assert.That(Clip(player), Is.EqualTo(ContactClip.Idle));
    }

    [Test]
    public void AnAllowedJumpFadesWithItsRule()
    {
        Entity player = Player(Floor);
        AnimatorRuntime runtime = player.GetComponent<CharacterRig>().Runtime;
        XafTransitions.Allow(runtime.Package, ContactClip.Idle.Id(), ContactClip.Jumping.Id()).Fade = 0.4f;
        Frame();

        MoveTo(player, Floor - 50);
        Frame();

        Assert.That(Clip(player), Is.EqualTo(ContactClip.Jumping));
        Assert.That(runtime.FadeDuration, Is.EqualTo(0.4f));
    }

    private static void Slide(Entity player, float pixelsPerSecond) =>
        player.GetComponent<PhysicsBody>().Body.SetLinearVelocity(
            new System.Numerics.Vector2(pixelsPerSecond / Box2DPhysicsWorld.PixelsPerMeter, 0f));

    [TestCase(CharacterMotionSystem.SlideStartSpeed)]
    [TestCase(-CharacterMotionSystem.SlideStartSpeed)]
    public void MovingSidewaysOnTheFloorPlaysTheSlideClipInEitherDirection(float speed)
    {
        Entity player = Player(Floor);
        Frame();
        Slide(player, speed);
        Frame();

        Assert.That(State(player), Is.EqualTo(CharacterMotionState.Sliding));
        Assert.That(Clip(player), Is.EqualTo(ContactClip.Sliding));
    }

    [Test]
    public void ASlideLastsUntilTheSpeedFallsBelowTheLowerStopSpeed()
    {
        Entity player = Player(Floor);
        Frame();
        Slide(player, CharacterMotionSystem.SlideStartSpeed - 1f);
        Frame();
        Assert.That(State(player), Is.EqualTo(CharacterMotionState.Grounded), "too slow to start");

        Slide(player, CharacterMotionSystem.SlideStartSpeed);
        Frame();
        Slide(player, CharacterMotionSystem.SlideStopSpeed);
        Frame();
        Assert.That(State(player), Is.EqualTo(CharacterMotionState.Sliding), "fast enough to keep going");

        Slide(player, CharacterMotionSystem.SlideStopSpeed - 1f);
        Frame();
        Assert.That(State(player), Is.EqualTo(CharacterMotionState.Grounded));
        Assert.That(Clip(player), Is.EqualTo(ContactClip.Idle));
    }

    [Test]
    public void TouchingDownWithSpeedLandsFirstAndThenSlides()
    {
        Entity player = Player(Floor - 200);
        Frame(2);
        Slide(player, 300f);
        MoveTo(player, Floor);
        Frame();
        Assert.That(State(player), Is.EqualTo(CharacterMotionState.Landing));

        // The landing clip is 0.25 s long.
        Frame(30);

        Assert.That(State(player), Is.EqualTo(CharacterMotionState.Sliding));
    }

    [Test]
    public void MovingSidewaysInTheAirIsNotASlide()
    {
        Entity player = Player(Floor - 200);
        Frame();
        Slide(player, 300f);
        Frame(2);

        Assert.That(State(player), Is.EqualTo(CharacterMotionState.Falling));
    }

    [Test]
    public void RisingEndsASlide()
    {
        Entity player = Player(Floor);
        Frame();
        Slide(player, 300f);
        Frame();

        MoveTo(player, Floor - 150);
        Frame();

        Assert.That(State(player), Is.EqualTo(CharacterMotionState.Jumping));
    }

    [Test]
    public void ARigWithoutASlideClipStaysIdleWhileSliding()
    {
        Entity player = Player(Floor);
        player.GetComponent<CharacterRig>().Runtime.Package.Clips.RemoveAll(clip => clip.Id == ContactClip.Sliding.Id());
        Frame();
        Slide(player, 300f);
        Frame();

        Assert.That(State(player), Is.EqualTo(CharacterMotionState.Sliding));
        Assert.That(Clip(player), Is.EqualTo(ContactClip.Idle));
    }

    private static XafPackage Package(bool falling)
    {
        var package = new XafPackage();
        package.Skeleton.Bones.Add(new XafBone { Id = "root", Parent = -1, Rest = Transform2D.Identity });
        package.Clips.Add(new XafClip { Id = ContactClip.Idle.Id(), Duration = 2f, Loop = true, LoopEnd = 2f });
        package.Clips.Add(new XafClip { Id = ContactClip.Jumping.Id(), Duration = 0.5f });
        package.Clips.Add(new XafClip { Id = ContactClip.Landing.Id(), Duration = 0.25f });
        package.Clips.Add(new XafClip { Id = ContactClip.Sliding.Id(), Duration = 0.4f, Loop = true, LoopEnd = 0.4f });
        if (falling)
            package.Clips.Add(new XafClip { Id = ContactClip.Falling.Id(), Duration = 1f, Loop = true, LoopEnd = 1f });
        return package;
    }
}

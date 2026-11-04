using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Tools.Animator;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Systems;

namespace Xanadu.Tests.Animation.Tool;

[TestFixture]
public sealed class AnimatorAttackCycleTests : AnimatorToolTest
{
    private const float AttackSeconds = 0.25f;

    protected override void AddSystems()
    {
        Root.Add(new AnimatorActionSystem());
        Root.Add(new AnimatorSequenceSystem());
        Root.Add(new AnimatorAttackCycleSystem());
        Root.Add(new AnimatorPlaybackSystem());
    }

    /// <summary>Idle loops; the rig has a jump but no fall, and one grounded and one air attack.</summary>
    protected override XafPackage CreatePackage()
    {
        XafPackage package = base.CreatePackage();
        package.Clips[0].Loop = true;
        package.Clips[0].LoopEnd = 2f;
        package.Clips.Add(new XafClip { Id = ContactClip.Jumping.Id(), Duration = 1f });
        package.Clips.Add(new XafClip { Id = ContactClip.AttackMid.Id(), Duration = AttackSeconds });
        package.Clips.Add(new XafClip { Id = ContactClip.AttackAirHigh.Id(), Duration = AttackSeconds });
        return package;
    }

    private AnimatorAttackCycle Cycle => Session.GetComponent<AnimatorAttackCycle>();

    private string Layer => Runtime.Additives.Count == 1 ? Runtime.Additives[0].Clip.Id : $"{Runtime.Additives.Count} layers";

    [Test]
    public void TheOrderHasTheAttacksTheRigHasHighToLowGroundThenAir()
    {
        Assert.That(AnimatorAttackCycleCommands.Order(Package),
            Is.EqualTo(new[] { ContactClip.AttackMid, ContactClip.AttackAirHigh }));
        Assert.That(AnimatorAttackCycleCommands.Base(Package, ContactClip.AttackMid), Is.EqualTo("idle"));

        // No falling clip, so the air attacks play over the held jump, as in the game.
        Assert.That(AnimatorAttackCycleCommands.Base(Package, ContactClip.AttackAirHigh), Is.EqualTo("jumping"));
    }

    private float Beat => Session.GetComponent<AnimatorMetronome>().BeatSeconds;

    [Test]
    public void EachAttackFiresOnItsBeatOverItsBaseAndTheCycleComesRound()
    {
        Session.GetComponent<AnimatorMetronome>() = new AnimatorMetronome { Bpm = 120, Every = 2 };
        Runtime.Play("jump", 0f);
        AnimatorAttackCycleCommands.Toggle(Session);

        Assert.That(Runtime.CurrentClip, Is.EqualTo("idle"));
        Assert.That(Layer, Is.EqualTo(ContactClip.AttackMid.Id()));
        Assert.That(Runtime.Additives[0].Blend, Is.EqualTo(XafLayerBlend.Override));

        // The beat between attacks only clicks.
        Assert.That(AnimatorAttackCycleCommands.Tick(Session, Beat), Is.EqualTo(AnimatorBeat.Beat));
        Assert.That(Cycle.Index, Is.EqualTo(0));

        Assert.That(AnimatorAttackCycleCommands.Tick(Session, Beat), Is.EqualTo(AnimatorBeat.Hit));
        Assert.That(Cycle.Index, Is.EqualTo(1));
        Assert.That(Layer, Is.EqualTo(ContactClip.AttackAirHigh.Id()));
        Assert.That(Runtime.IsCrossfading ? Runtime.NextClip : Runtime.CurrentClip, Is.EqualTo("jumping"));

        AnimatorAttackCycleCommands.Tick(Session, Beat);
        AnimatorAttackCycleCommands.Tick(Session, Beat);
        Assert.That(Cycle.Index, Is.EqualTo(0));
        Assert.That(Layer, Is.EqualTo(ContactClip.AttackMid.Id()));
    }

    [Test]
    public void AnAttackStartsFromItsContactEvent()
    {
        XafClip mid = Package.Clips.Find(clip => clip.Id == ContactClip.AttackMid.Id())!;
        XafEventAuthoring.Add(mid, 0.1f);

        AnimatorAttackCycleCommands.Toggle(Session);

        Assert.That(Runtime.Layer(mid.Id)!.Value.Time, Is.EqualTo(0.1f));
    }

    [Test]
    public void EditingAnAttackFiresOnlyThatOne()
    {
        Runtime.Play(ContactClip.AttackAirHigh.Id(), 0f);

        AnimatorAttackCycleCommands.Toggle(Session);

        Assert.That(Cycle.Order, Is.EqualTo(new[] { ContactClip.AttackAirHigh }));
        Assert.That(Layer, Is.EqualTo(ContactClip.AttackAirHigh.Id()));
    }

    [Test]
    public void TheTempoAndSpacingStepWithinTheirLimits()
    {
        for (int i = 0; i < 100; i++)
            AnimatorAttackCycleCommands.StepBpm(Session, 1);
        AnimatorAttackCycleCommands.StepEvery(Session, -1);
        AnimatorAttackCycleCommands.StepEvery(Session, -1);

        Assert.That(Session.GetComponent<AnimatorMetronome>().Bpm, Is.EqualTo(AnimatorMetronome.MaxBpm));
        Assert.That(Session.GetComponent<AnimatorMetronome>().Every, Is.EqualTo(1));
    }

    [Test]
    public void TurningItOffTakesTheLayerOffAndGoesBackToTheClipBeingEdited()
    {
        Runtime.Play("jump", 0f);
        Runtime.Seek(0.4f);
        AnimatorAttackCycleCommands.Toggle(Session);
        Frame(5);

        AnimatorAttackCycleCommands.Toggle(Session);

        Assert.That(Cycle.Active, Is.False);
        Assert.That(Runtime.Additives, Is.Empty);
        Assert.That(Runtime.CurrentClip, Is.EqualTo("jump"));
        Assert.That(Runtime.CurrentTime, Is.EqualTo(0.4f).Within(0.0001f));
        Assert.That(Runtime.Paused, Is.True);
    }

    [Test]
    public void PickingAClipByHandEndsTheCycleWithoutTouchingWhatPlays()
    {
        AnimatorAttackCycleCommands.Toggle(Session);
        Runtime.Play("jump", 0f);

        Frame();

        Assert.That(Cycle.Active, Is.False);
        Assert.That(Runtime.Additives, Is.Empty);
        Assert.That(Runtime.CurrentClip, Is.EqualTo("jump"));
    }

    [Test]
    public void StartingAChainEndsTheCycle()
    {
        AnimatorAttackCycleCommands.Toggle(Session);

        AnimatorSequenceCommands.StartAll(Session);

        Assert.That(Cycle.Active, Is.False);
        Assert.That(Runtime.Additives, Is.Empty);
        Assert.That(Session.GetComponent<AnimatorSequence>().Active, Is.True);
    }

    [Test]
    public void ARigWithoutAttacksSaysSo()
    {
        Package.Clips.RemoveAll(clip => clip.Id.StartsWith("attack"));

        AnimatorAttackCycleCommands.Toggle(Session);

        Assert.That(Cycle.Active, Is.False);
        Assert.That(Session.GetComponent<AnimatorStatus>().Message, Does.Contain("NO ATTACK CLIPS"));
    }

    [Test]
    public void ThePreviewBarHasTheToggle()
    {
        Assert.That(Button(ToolActionKind.CycleAttacks).IsNull, Is.False);
    }
}

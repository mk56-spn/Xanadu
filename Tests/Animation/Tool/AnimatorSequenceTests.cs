using System.Collections.Generic;
using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Tools.Animator;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Systems;

namespace Xanadu.Tests.Animation.Tool;

[TestFixture]
public sealed class AnimatorSequenceTests : AnimatorToolTest
{
    protected override void AddSystems()
    {
        Root.Add(new AnimatorActionSystem());
        Root.Add(new AnimatorSequenceSystem());
        Root.Add(new AnimatorPlaybackSystem());
    }

    /// <summary>Clips: idle 2 s (looping), jump 1 s, then jumping 1 s, landing 0.4 s.</summary>
    protected override XafPackage CreatePackage()
    {
        XafPackage package = base.CreatePackage();
        package.Clips[0].Loop = true;
        package.Clips[0].LoopEnd = 2f;
        package.Clips.Add(new XafClip { Id = "jumping", Duration = 1f });
        package.Clips.Add(new XafClip { Id = "landing", Duration = 0.4f });
        return package;
    }

    private AnimatorSequence Sequence => Session.GetComponent<AnimatorSequence>();

    private static string Names(List<AnimatorSequenceStep> steps) =>
        string.Join(">", steps.ConvertAll(step => step.Clip));

    [Test]
    public void TheGameChainFollowsTheGamesStatesWithTheClipsTheRigHas()
    {
        List<AnimatorSequenceStep> steps = AnimatorSequenceCommands.GameSteps(Package);

        // No falling clip here, so the jump pose is kept and the two steps merge.
        Assert.That(Names(steps), Is.EqualTo("jumping>landing>idle"));
        Assert.That(steps[0].Seconds, Is.EqualTo(
            AnimatorSequenceCommands.JumpSeconds + AnimatorSequenceCommands.FallSeconds).Within(0.0001f));
        Assert.That(steps[1].Seconds, Is.EqualTo(0.4f).Within(0.0001f));
    }

    [Test]
    public void AFallingClipGetsItsOwnStep()
    {
        Package.Clips.Add(new XafClip { Id = "falling", Duration = 0.5f });

        Assert.That(Names(AnimatorSequenceCommands.GameSteps(Package)), Is.EqualTo("jumping>falling>landing>idle"));
    }

    [Test]
    public void TheAllChainPlaysEveryClipInFileOrderAndALoopOnce()
    {
        List<AnimatorSequenceStep> steps = AnimatorSequenceCommands.AllSteps(Package);

        Assert.That(Names(steps), Is.EqualTo("idle>jump>jumping>landing"));
        Assert.That(steps[0].Seconds, Is.EqualTo(2f).Within(0.0001f));
    }

    [Test]
    public void StartingACrossfadesFromOneClipIntoTheNextAndFinishesOnTheLast()
    {
        Click(Button(ToolActionKind.SequenceAll));
        Assert.That(Runtime.CurrentClip, Is.EqualTo("idle"));
        Assert.That(Sequence.Active, Is.True);

        // Idle plays for 2 s, then fades into "jump".
        Frame(125);
        Assert.That(Runtime.CurrentClip == "jump" || Runtime.NextClip == "jump", Is.True);
        Assert.That(Sequence.Index, Is.EqualTo(1));

        // jump 1 s, jumping 1 s, landing 0.4 s: well past the end of the chain.
        Frame(60 * 3);
        Assert.That(Sequence.Active, Is.False);
        Assert.That(Session.GetComponent<AnimatorStatus>().Message, Does.Contain("DONE"));
    }

    [Test]
    public void WhenTheChainIsDoneTheEditorIsBackOnTheClipAndTimeItStartedFrom()
    {
        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.Clip, "jump"));
        Runtime.Seek(0.6f);
        Runtime.SetPaused(true);

        Click(Button(ToolActionKind.SequenceAll));
        Assert.That(Runtime.CurrentClip, Is.EqualTo("idle"));
        Frame(60 * 6);

        Assert.That(Sequence.Active, Is.False);
        Assert.That(Runtime.CurrentClip, Is.EqualTo("jump"));
        Assert.That(Runtime.CurrentTime, Is.EqualTo(0.6f).Within(0.0001f));
        Assert.That(Runtime.IsCrossfading, Is.False);
        Assert.That(Runtime.Paused, Is.True);
    }

    [Test]
    public void ThePreviewBlendsOverTheCrossfadeInsteadOfSnapping()
    {
        Click(Button(ToolActionKind.SequenceAll));
        Frame(60 * 2 - 3);
        Assert.That(Runtime.IsCrossfading, Is.False);

        Frame(4);

        Assert.That(Runtime.IsCrossfading, Is.True);
        Assert.That(Runtime.NextClip, Is.EqualTo("jump"));
    }

    [Test]
    public void PickingAClipByHandEndsTheChain()
    {
        Click(Button(ToolActionKind.SequenceAll));
        Frame(10);

        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.Clip, "landing"));
        Frame(2);

        Assert.That(Sequence.Active, Is.False);
        Assert.That(Runtime.CurrentClip, Is.EqualTo("landing"));
    }

    [Test]
    public void StopEndsTheChain()
    {
        Click(Button(ToolActionKind.SequenceGame));
        Assert.That(Sequence.Active, Is.True);

        Click(Button(ToolActionKind.Stop));

        Assert.That(Sequence.Active, Is.False);
        Assert.That(Runtime.Paused, Is.True);
    }

    [Test]
    public void StoppingPartWayAlsoPutsTheEditorBack()
    {
        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.Clip, "landing"));
        Runtime.Seek(0.2f);

        Click(Button(ToolActionKind.SequenceGame));
        Frame(20);
        Assert.That(Runtime.CurrentClip, Is.Not.EqualTo("landing"));
        Click(Button(ToolActionKind.Stop));

        Assert.That(Runtime.CurrentClip, Is.EqualTo("landing"));
        Assert.That(Runtime.CurrentTime, Is.EqualTo(0.2f).Within(0.0001f));
    }

    [Test]
    public void PausingHoldsTheChainWhereItIs()
    {
        Click(Button(ToolActionKind.SequenceAll));
        Frame(30);
        Runtime.SetPaused(true);
        float before = Sequence.Elapsed;

        Frame(120);

        Assert.That(Sequence.Active, Is.True);
        Assert.That(Sequence.Elapsed, Is.EqualTo(before).Within(0.0001f));
    }

    [Test]
    public void ARigWithNothingToChainSaysSo()
    {
        Package.Clips.RemoveAll(clip => clip.Id != "idle");
        Runtime.Play("idle", 0f);

        AnimatorSequenceCommands.StartGame(Session);

        Assert.That(Sequence.Active, Is.False);
        Assert.That(Session.GetComponent<AnimatorStatus>().Message, Does.Contain("AT LEAST TWO"));
    }
}

using System.Collections.Generic;
using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Tests.Support;
using Xanadu.Theme;
using Xanadu.Tools.Animator;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Systems;

namespace Xanadu.Tests.Animation.Tool;

/// <summary>
/// The FLOW tab: allowing the edited clip to fade into others, tuning each
/// fade, and previewing a hand-over the way the game plays it.
/// </summary>
[TestFixture]
public sealed class AnimatorFlowTests : AnimatorToolTest
{
    protected override void AddSystems()
    {
        Root.Add(new AnimatorActionSystem());
        Root.Add(new AnimatorSequenceSystem());
        Root.Add(new AnimatorPlaybackSystem());
        Root.Add(new AnimatorFlowSystem(new FakeVisualRidFactory()));
    }

    /// <summary>Clips: idle 2 s, jump 1 s, landing 0.4 s.</summary>
    protected override XafPackage CreatePackage()
    {
        XafPackage package = base.CreatePackage();
        package.Clips.Add(new XafClip { Id = "landing", Duration = 0.4f });
        return package;
    }

    private AnimatorSequence Sequence => Session.GetComponent<AnimatorSequence>();
    private string Status => Session.GetComponent<AnimatorStatus>().Message;
    private static string Label(Entity entity) => entity.GetComponent<Text>().Value;

    [Test]
    public void TheListHasARowForEveryOtherClipMarkedWithTheGamesHandOvers()
    {
        // In the order the list shows them, not the order a query happens to return.
        var rows = new List<Entity>();
        foreach (Entity row in Ui.FlowList.ChildEntities)
        {
            foreach (Entity button in row.ChildEntities)
            {
                if (button.GetComponent<ToolAction>().Kind == ToolActionKind.SelectTransition)
                    rows.Add(button);
            }
        }

        Assert.That(rows.ConvertAll(row => row.GetComponent<ToolAction>().Argument),
            Is.EqualTo(new[] { "jump", "landing" }));
        Assert.That(Label(Ui.FlowTitle), Is.EqualTo("FROM IDLE"));

        // This rig has no "jumping" clip, so the game's only hand-over from idle is none.
        Assert.That(Label(rows[0]), Is.EqualTo("JUMP"));
        Assert.That(Label(Button(ToolActionKind.AllowTransition, "jump")), Is.EqualTo("ANY"));
    }

    [Test]
    public void AllowingAClipAddsARuleAndSelectsIt()
    {
        Click(Button(ToolActionKind.AllowTransition, "jump"));

        Assert.That(XafTransitions.Find(Package, "idle", "jump"), Is.Not.Null);
        Assert.That(Session.GetComponent<AnimatorFlow>().Target, Is.EqualTo("jump"));
        Assert.That(Status, Does.Contain("OTHER HAND-OVERS NOW NEED A RULE"));
        Assert.That(Label(Button(ToolActionKind.AllowTransition, "jump")), Is.EqualTo("ON"));
        Assert.That(Label(Button(ToolActionKind.AllowTransition, "landing")), Is.EqualTo("OFF"));
        Assert.That(Label(Ui.FlowRule), Is.EqualTo("IDLE ▸ JUMP"));

        Click(Button(ToolActionKind.Undo));
        Assert.That(XafTransitions.IsOpen(Package), Is.True);
    }

    [Test]
    public void TheFadeStepsOnARoundGridAndTheCurveIsSet()
    {
        Click(Button(ToolActionKind.AllowTransition, "jump"));
        Click(Button(ToolActionKind.TransitionFadeUp));
        Click(Button(ToolActionKind.TransitionFadeUp));
        Click(Button(ToolActionKind.TransitionCurve, nameof(XafFadeCurve.Smooth)));

        XafTransition rule = XafTransitions.Find(Package, "idle", "jump")!;
        Assert.That(rule.Fade, Is.EqualTo(0.2f).Within(0.0001f));
        Assert.That(rule.Curve, Is.EqualTo(XafFadeCurve.Smooth));
        Assert.That(Label(Ui.FlowFade), Is.EqualTo("0.20 s  SMOOTH"));
        Assert.That(Label(Button(ToolActionKind.SelectTransition, "jump")), Is.EqualTo("JUMP  0.20s SMO"));
        Assert.That(Button(ToolActionKind.TransitionCurve, nameof(XafFadeCurve.Smooth))
            .GetComponent<ButtonVisual>().Normal, Is.EqualTo(Palette.Selected));
    }

    [Test]
    public void TheFadeCannotBeTunedBeforeTheHandOverIsAllowed()
    {
        Click(Button(ToolActionKind.SelectTransition, "jump"));
        Click(Button(ToolActionKind.TransitionFadeUp));

        Assert.That(XafTransitions.IsOpen(Package), Is.True);
        Assert.That(Status, Is.EqualTo("ALLOW A TRANSITION FIRST"));
    }

    [Test]
    public void PreviewPlaysTheClipIntoTheTargetWithItsFadeAndReturns()
    {
        Click(Button(ToolActionKind.AllowTransition, "jump"));
        XafTransitions.Find(Package, "idle", "jump")!.Fade = 0.5f;
        Click(Button(ToolActionKind.PreviewTransition));

        Assert.That(Sequence.Active, Is.True);
        Assert.That(Sequence.Name, Is.EqualTo("FLOW"));
        Assert.That(Sequence.Steps!.ConvertAll(step => step.Clip), Is.EqualTo(new[] { "idle", "jump" }));

        // idle plays its full 2 s, then fades into jump over the rule's 0.5 s.
        Frame(125);
        Assert.That(Runtime.NextClip, Is.EqualTo("jump"));
        Assert.That(Runtime.FadeDuration, Is.EqualTo(0.5f));
        Assert.That(Label(Ui.FlowTitle), Does.StartWith("FROM IDLE"), "the tab stays on the clip being edited");

        Frame(60 * 2);
        Assert.That(Sequence.Active, Is.False);
        Assert.That(Runtime.CurrentClip, Is.EqualTo("idle"));
    }

    [Test]
    public void ALoopedPreviewStartsOverInsteadOfEnding()
    {
        Click(Button(ToolActionKind.SelectTransition, "landing"));
        Click(Button(ToolActionKind.ToggleFlowLoop));
        Click(Button(ToolActionKind.PreviewTransition));

        Frame(60 * 5);

        Assert.That(Sequence.Active, Is.True);
        Assert.That(Sequence.Loop, Is.True);
    }

    [Test]
    public void SlowMotionPlaysAtAQuarterOfTheSpeed()
    {
        Click(Button(ToolActionKind.ToggleSlow));
        Runtime.Seek(0f);
        Click(Button(ToolActionKind.Play));
        float start = Runtime.CurrentTime;

        Frame(60);

        Assert.That(Runtime.CurrentTime - start, Is.EqualTo(AnimatorFlow.SlowSpeed).Within(0.05f));
    }

    [Test]
    public void TheGameChainRefusesAHandOverTheTableDoesNotAllow()
    {
        Package.Clips.Add(new XafClip { Id = "jumping", Duration = 0.3f });
        AnimatorSession.RigChanged(Session);
        XafTransitions.Allow(Package, "jumping", "idle");
        AnimatorSequenceCommands.StartGame(Session);
        Assert.That(Sequence.Steps!.ConvertAll(step => step.Clip), Is.EqualTo(new[] { "jumping", "landing", "idle" }));

        // Past the jump and fall: jumping may not fade into landing, so it plays on.
        Frame(50);
        Assert.That(Runtime.CurrentClip, Is.EqualTo("jumping"));
        Assert.That(Runtime.IsCrossfading, Is.False);
        Assert.That(Status, Does.Contain("NOT ALLOWED"));
    }

    [Test]
    public void TheGapRatingWeighsTheJumpAgainstTheFadeAndItsCurve()
    {
        Assert.That(AnimatorFlowSystem.Rate(4f, 0f, XafFadeCurve.Linear).Rating, Is.EqualTo("SMOOTH"));
        Assert.That(AnimatorFlowSystem.Rate(40f, 0.1f, XafFadeCurve.Linear).Rating, Is.EqualTo("SMOOTH"));
        Assert.That(AnimatorFlowSystem.Rate(40f, 0.1f, XafFadeCurve.EaseIn).Rating, Does.StartWith("VISIBLE"));
        Assert.That(AnimatorFlowSystem.Rate(200f, 0.05f, XafFadeCurve.Linear).Rating, Does.StartWith("POPS"));
    }
}

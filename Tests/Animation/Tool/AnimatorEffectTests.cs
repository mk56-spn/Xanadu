using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Tests.Support;
using Xanadu.Tools.Animator;
using Xanadu.Tools.Animator.Systems;

namespace Xanadu.Tests.Animation.Tool;

/// <summary>Adding a streak to a bone in the animator, then keying when it shows, how opaque, and where.</summary>
[TestFixture]
public sealed class AnimatorEffectTests : AnimatorToolTest
{
    protected override void AddSystems()
    {
        Root.Add(new AnimatorActionSystem());
        Root.Add(new AnimatorHierarchySystem(new FakeVisualRidFactory()));
    }

    private string Status => Session.GetComponent<AnimatorStatus>().Message;
    private AnimatorSelection Selection => Session.GetComponent<AnimatorSelection>();

    private void AddStreakOnTorsoAt(float seconds)
    {
        Session.GetComponent<AnimatorSelection>().SelectOnlyBone(1);
        Runtime.Seek(seconds);
        Click(Button(ToolActionKind.AddEffect));
    }

    [Test]
    public void AStreakNeedsABone()
    {
        Click(Button(ToolActionKind.AddEffect));

        Assert.That(Package.Effects, Is.Empty);
        Assert.That(Status, Is.EqualTo("SELECT A BONE FIRST"));
    }

    [Test]
    public void AddingAStreakShowsItBrieflyFromThePlayheadAndSelectsIt()
    {
        AddStreakOnTorsoAt(0.5f);

        Assert.That(Package.Effects, Has.Count.EqualTo(1));
        Assert.That(Package.Effects[0].BoneId, Is.EqualTo("torso"));
        Assert.That(Selection.Effect, Is.EqualTo(0));
        Assert.That(Selection.Bone, Is.EqualTo(-1));

        Assert.That(XafSampler.Sample(Package, Idle, 0.45f).Effects[0].Visible, Is.False);
        Assert.That(XafSampler.Sample(Package, Idle, 0.55f).Effects[0].Visible, Is.True);
        Assert.That(XafSampler.Sample(Package, Idle, 0.5f + XafEffects.DefaultShowSeconds).Effects[0].Visible, Is.False);

        Assert.That(Button(ToolActionKind.SelectEffect, "0").GetComponent<Text>().Value, Is.EqualTo("streak  on torso"));
        Assert.That(Runtime.Pose.Effects, Has.Length.EqualTo(1), "the runtime is rebuilt for the new effect");
    }

    [Test]
    public void ShowHideKeysTheOtherWayFromWhatIsShowing()
    {
        AddStreakOnTorsoAt(0.5f);
        Runtime.Seek(1.5f);
        Assert.That(Runtime.Pose.Effects[0].Visible, Is.False);

        Click(Button(ToolActionKind.ToggleEffectVisible));

        Assert.That(Runtime.Pose.Effects[0].Visible, Is.True);
        Assert.That(Status, Does.Contain("SHOWN"));
    }

    [Test]
    public void FadeStepsKeyTheOpacityAndTheTimelineHasTheEffectsRows()
    {
        AddStreakOnTorsoAt(0.5f);
        Runtime.Seek(0.55f);

        Click(Button(ToolActionKind.EffectOpacityDown));
        Click(Button(ToolActionKind.EffectOpacityDown));

        Assert.That(Runtime.Pose.Effects[0].Opacity, Is.EqualTo(0.5f).Within(0.001f));
        var track = new XafTrack(XafChannelTarget.Effect, "streak");
        Assert.That(XafTracks.Of(Package, Idle), Does.Contain(track));
        Assert.That(AnimatorTrackRows.Channels(Idle, track), Does.Contain(XafChannelProperty.Opacity));
        Assert.That(AnimatorTrackRow.PropertyLabel(XafChannelProperty.Visible), Is.EqualTo("show"));
    }

    [Test]
    public void TurningTheSelectedEffectKeysItAndDeletingTakesItsKeys()
    {
        AddStreakOnTorsoAt(0.5f);
        Runtime.Seek(0.55f);
        Click(Button(ToolActionKind.RotateRight));
        Click(Button(ToolActionKind.KeyBone));

        Assert.That(XafSampler.Sample(Package, Idle, 0.55f).Effects[0].Transform.Rotation,
            Is.EqualTo(Tools.Animator.Commands.AnimatorEditing.RotationStep).Within(0.001f));

        Click(Button(ToolActionKind.DeleteEffect));
        Assert.That(Package.Effects, Is.Empty);
        Assert.That(Idle.Channels.Exists(channel => channel.Target == XafChannelTarget.Effect), Is.False);

        Click(Button(ToolActionKind.Undo));
        Assert.That(Package.Effects, Has.Count.EqualTo(1));
    }
}

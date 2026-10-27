using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Systems.Ui;
using Xanadu.Tests.Support;
using Xanadu.Tools.Animator;
using Xanadu.Tools.Animator.Systems;

namespace Xanadu.Tests.Animation.Tool;

[TestFixture]
public sealed class AnimatorActionTests : AnimatorToolTest
{
    protected override void AddSystems()
    {
        Root.Add(new ToggleSystem());
        Root.Add(new UiTreeSystem());
        Root.Add(new AnimatorActionSystem());
        Root.Add(new AnimatorModeSystem());
        Root.Add(new AnimatorHierarchySystem(new FakeVisualRidFactory()));
    }

    [Test]
    public void PressingPlayStartsPlaybackAndStopPausesIt()
    {
        Runtime.SetPaused(true);

        Click(Button(ToolActionKind.Play));
        Assert.That(Runtime.Paused, Is.False);

        Click(Button(ToolActionKind.Stop));
        Assert.That(Runtime.Paused, Is.True);
    }

    [Test]
    public void HoldingAButtonRunsItsActionOnce()
    {
        Entity key = Button(ToolActionKind.AddBone);
        key.AddTag<Pressed>();
        Frame(5);

        Assert.That(Package.Skeleton.Bones, Has.Count.EqualTo(4));
    }

    [Test]
    public void ClipButtonSwitchesClipAndResumes()
    {
        Runtime.SetPaused(true);

        Click(Button(ToolActionKind.Clip, "jump"));

        Assert.That(Runtime.CurrentClip, Is.EqualTo("jump"));
        Assert.That(Runtime.IsCrossfading, Is.False);
        Assert.That(Runtime.Paused, Is.False);
    }

    [Test]
    public void SelectingABoneUpdatesSelectionAndClearsAttachment()
    {
        Click(Button(ToolActionKind.SelectAttachment, "0"));
        Click(BoneLabel("head"));

        AnimatorSelection selection = Session.GetComponent<AnimatorSelection>();
        Assert.That(selection.Bone, Is.EqualTo(2));
        Assert.That(selection.Attachment, Is.EqualTo(-1));
    }

    [Test]
    public void SelectingAnAttachmentClearsBone()
    {
        Click(BoneLabel("torso"));
        Click(Button(ToolActionKind.SelectAttachment, "0"));

        AnimatorSelection selection = Session.GetComponent<AnimatorSelection>();
        Assert.That(selection.Attachment, Is.EqualTo(0));
        Assert.That(selection.Bone, Is.EqualTo(-1));
    }

    [Test]
    public void FileMenuOptionsRunTheirActions()
    {
        Files.ToLoad = new XafPackage();
        Files.ToLoad.Skeleton.Bones.Add(new XafBone { Id = "only", Parent = -1 });

        XafPackage original = Package;

        Click(Button(ToolActionKind.Save));
        Assert.That(Files.Saved, Is.SameAs(original));

        Click(Button(ToolActionKind.Load));
        Assert.That(Package.Skeleton.Bones, Has.Count.EqualTo(1));
    }

    [Test]
    public void ClickingAModeToggleTurnsTheModeOn()
    {
        Click(ModeButton(AnimatorMode.AutoKey));

        Assert.That(Session.GetComponent<AnimatorModes>().AutoKey, Is.True);
        Assert.That(ModeButton(AnimatorMode.AutoKey).Tags.Has<ToggleChanged>(), Is.False);

        Click(ModeButton(AnimatorMode.AutoKey));
        Assert.That(Session.GetComponent<AnimatorModes>().AutoKey, Is.False);
    }

    [Test]
    public void EnteringRestPoseFreezesPlayback()
    {
        Runtime.SetPaused(false);

        Click(ModeButton(AnimatorMode.Rest));

        Assert.That(Session.GetComponent<AnimatorModes>().Rest, Is.True);
        Assert.That(Runtime.Paused, Is.True);
    }

    [Test]
    public void ToggleFollowsAModeChangedByACommand()
    {
        Click(ModeButton(AnimatorMode.Rest));
        Assert.That(ModeButton(AnimatorMode.Rest).GetComponent<Toggle>().Value, Is.True);

        Click(Button(ToolActionKind.NewSkeleton));

        Assert.That(Session.GetComponent<AnimatorModes>().Rest, Is.False);
        Assert.That(ModeButton(AnimatorMode.Rest).GetComponent<Toggle>().Value, Is.False);
    }
}

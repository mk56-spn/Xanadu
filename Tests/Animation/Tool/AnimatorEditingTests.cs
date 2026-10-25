using Godot;
using NUnit.Framework;
using Writer.Animation;
using Writer.Tools.Animator;
using Writer.Tools.Animator.Commands;

namespace Writer.Tests.Animation.Tool;

[TestFixture]
public sealed class AnimatorEditingTests : AnimatorToolTest
{
    private void Select(int bone) =>
        Session.GetComponent<AnimatorSelection>().Bone = bone;

    [Test]
    public void RotateButtonsDoNotChangeScale()
    {
        Select(1);

        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.RotateRight));

        XafTransform torso = Runtime.Pose.Bones[1];
        Assert.That(torso.Rotation, Is.EqualTo(AnimatorEditing.RotationStep).Within(0.0001f));
        Assert.That(torso.Scale, Is.EqualTo(Vector2.One));
    }

    [Test]
    public void ScaleButtonsDoNotChangeRotation()
    {
        Select(1);

        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.ScaleUp));

        XafTransform torso = Runtime.Pose.Bones[1];
        Assert.That(torso.Rotation, Is.EqualTo(0f));
        Assert.That(torso.Scale.X, Is.EqualTo(1f + AnimatorEditing.ScaleStep).Within(0.0001f));
    }

    [Test]
    public void NudgingInRestPoseEditsTheRestTransform()
    {
        Select(1);
        Session.GetComponent<AnimatorModes>().Rest = true;

        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.RotateLeft));

        Assert.That(Package.Skeleton.Bones[1].Rest.Rotation,
            Is.EqualTo(-AnimatorEditing.RotationStep).Within(0.0001f));
        Assert.That(Runtime.Pose.Bones[1].Rotation, Is.EqualTo(0f));
    }

    [Test]
    public void NudgingWithAutoKeyKeysTheBoneAtThePlayhead()
    {
        Select(1);
        Session.GetComponent<AnimatorModes>().AutoKey = true;
        Runtime.Seek(0.5f);

        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.RotateRight));

        XafChannel? channel = Idle.Channels.Find(candidate =>
            candidate.TargetId == "torso" &&
            candidate.Property == XafChannelProperty.Rotation);
        Assert.That(channel, Is.Not.Null);
        Assert.That(channel!.Keyframes[0].Time, Is.EqualTo(0.5f));
    }

    [Test]
    public void KeyingInRestPoseIsRefused()
    {
        Select(1);
        Session.GetComponent<AnimatorModes>().Rest = true;
        int channels = Idle.Channels.Count;

        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.KeyBone));

        Assert.That(Idle.Channels, Has.Count.EqualTo(channels));
    }

    [Test]
    public void AddingABoneParentsItToTheSelectionAndBumpsTheVersion()
    {
        Select(2);
        int version = Session.GetComponent<AnimatorDocument>().Version;

        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.AddBone));

        Assert.That(Package.Skeleton.Bones, Has.Count.EqualTo(4));
        Assert.That(Package.Skeleton.Bones[3].Parent, Is.EqualTo(2));
        Assert.That(Session.GetComponent<AnimatorSelection>().Bone, Is.EqualTo(3));
        Assert.That(Session.GetComponent<AnimatorDocument>().Version, Is.EqualTo(version + 1));
    }

    [Test]
    public void DeletingABoneRemovesItsChannelsSoTheRigStillSaves()
    {
        XafAuthoring.SetBoneProperty(Package, Idle, "head",
            XafChannelProperty.TranslationX, 0.5f, 3f);
        Select(2);

        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.DeleteBone));

        Assert.That(Package.Skeleton.Bones, Has.Count.EqualTo(2));
        Assert.That(Idle.Channels.Exists(channel => channel.TargetId == "head"), Is.False);
        Assert.That(XafValidation.Validate(Package), Is.Empty);
        Assert.That(Session.GetComponent<AnimatorSelection>().Bone, Is.EqualTo(1));
    }

    [Test]
    public void RefusesToDeleteABoneWithChildren()
    {
        Select(1);

        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.DeleteBone));

        Assert.That(Package.Skeleton.Bones, Has.Count.EqualTo(3));
        Assert.That(Session.GetComponent<AnimatorStatus>().Message, Is.EqualTo("CANNOT DELETE BONE"));
    }

    [Test]
    public void SavingAnInvalidPackageReportsInsteadOfThrowing()
    {
        Files.FailSave = true;

        Assert.DoesNotThrow(() =>
            AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.Save)));

        Assert.That(Session.GetComponent<AnimatorStatus>().Message, Does.StartWith("SAVE FAILED"));
    }

    [Test]
    public void LoadingWithNothingToLoadKeepsThePackage()
    {
        XafPackage before = Package;
        Files.ToLoad = null;

        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.Load));

        Assert.That(Package, Is.SameAs(before));
        Assert.That(Session.GetComponent<AnimatorStatus>().Message, Is.EqualTo("LOAD FAILED: res://test.xaf"));
    }

    [Test]
    public void ReplacingThePackageResetsSelectionAndBumpsTheVersion()
    {
        Select(2);
        Session.GetComponent<AnimatorSelection>().HasKey = true;
        int version = Session.GetComponent<AnimatorDocument>().Version;
        Files.ToLoad = XafRigAuthoring.CreateEmptyPackage();

        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.Load));

        AnimatorSelection selection = Session.GetComponent<AnimatorSelection>();
        Assert.That(selection.Bone, Is.EqualTo(-1));
        Assert.That(selection.HasKey, Is.False);
        Assert.That(Session.GetComponent<AnimatorDocument>().Version, Is.EqualTo(version + 1));
        Assert.That(Runtime.CurrentClip, Is.EqualTo("idle"));
    }

    [Test]
    public void LoadingAPackageWithoutAnIdleClipPlaysItsFirstClip()
    {
        var other = XafRigAuthoring.CreateEmptyPackage();
        other.Clips.Clear();
        other.Clips.Add(new XafClip { Id = "walk", Duration = 1f });
        Files.ToLoad = other;

        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.Load));

        Assert.That(Runtime.CurrentClip, Is.EqualTo("walk"));
    }

    [Test]
    public void LoadingAPackageWithNoClipsDoesNotBreakPlayback()
    {
        var other = XafRigAuthoring.CreateEmptyPackage();
        other.Clips.Clear();
        Files.ToLoad = other;

        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.Load));

        Assert.DoesNotThrow(() =>
        {
            Runtime.SetPaused(false);
            Runtime.Update(0.5f);
        });
    }

    [Test]
    public void TogglingMovableFlipsTheFlagOnTheSelectedBone()
    {
        Select(1);

        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.ToggleMovable));
        Assert.That(Package.Skeleton.Bones[1].AllowTranslation, Is.True);
        Assert.That(Session.GetComponent<AnimatorStatus>().Message, Is.EqualTo("torso MOVABLE"));

        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.ToggleMovable));
        Assert.That(Package.Skeleton.Bones[1].AllowTranslation, Is.False);
    }

    [Test]
    public void TheRootIsAlwaysMovableAndKeepsItsFlagClear()
    {
        Select(0);

        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.ToggleMovable));

        Assert.That(Package.Skeleton.Bones[0].AllowTranslation, Is.False);
        Assert.That(Session.GetComponent<AnimatorStatus>().Message, Is.EqualTo("ROOT IS ALWAYS MOVABLE"));
    }
}

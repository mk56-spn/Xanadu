using System.Collections.Generic;
using Godot;
using NUnit.Framework;
using Writer.Animation;
using Writer.Ecs;
using Writer.Systems.Ui;
using Writer.Tests.Support;
using Writer.Tools.Animator;
using Writer.Tools.Animator.Commands;
using Writer.Tools.Animator.Systems;

namespace Writer.Tests.Animation.Tool;

[TestFixture]
public sealed class AnimatorHistoryTests : AnimatorToolTest
{
    protected override void AddSystems()
    {
        Root.Add(new UiTreeSystem());
        Root.Add(new AnimatorActionSystem());
        Root.Add(new AnimatorPreviewInputSystem(Pointer));
        Root.Add(new AnimatorTimelineInputSystem(Pointer, Pointer));
        Root.Add(new AnimatorShortcutSystem(Keyboard));
        Root.Add(new AnimatorHierarchySystem(new FakeVisualRidFactory()));
    }

    private void Run(ToolActionKind kind) =>
        AnimatorCommands.Run(Session, new ToolAction(kind));

    private void Select(int bone) =>
        Session.GetComponent<AnimatorSelection>().Bone = bone;

    private bool CanUndo => Session.GetComponent<AnimatorHistory>().History.CanUndo;

    private XafChannel? Channel(string bone, XafChannelProperty property) =>
        Idle.Channels.Find(channel =>
            channel.TargetId == bone && channel.Property == property);

    [Test]
    public void UndoingAKeyRemovesItAndRedoRestoresIt()
    {
        Select(1);
        Runtime.Seek(0.5f);

        Run(ToolActionKind.KeyBone);
        Assert.That(Channel("torso", XafChannelProperty.Rotation), Is.Not.Null);

        Run(ToolActionKind.Undo);
        Assert.That(Channel("torso", XafChannelProperty.Rotation), Is.Null);

        Run(ToolActionKind.Redo);
        Assert.That(Channel("torso", XafChannelProperty.Rotation), Is.Not.Null);
    }

    [Test]
    public void ANudgeWithAutoKeyIsASingleUndoStep()
    {
        Select(1);
        Session.GetComponent<AnimatorModes>().AutoKey = true;
        int channels = Idle.Channels.Count;

        Run(ToolActionKind.RotateRight);
        Assert.That(Idle.Channels.Count, Is.GreaterThan(channels));

        Run(ToolActionKind.Undo);
        Assert.That(Idle.Channels, Has.Count.EqualTo(channels));
        Assert.That(CanUndo, Is.False);
    }

    [Test]
    public void UndoingAddBoneRemovesItAndResizesThePose()
    {
        Select(2);
        Run(ToolActionKind.AddBone);

        Run(ToolActionKind.Undo);

        Assert.That(Package.Skeleton.Bones, Has.Count.EqualTo(3));
        Assert.That(Runtime.Pose.Bones.Length, Is.EqualTo(3));
        Assert.That(Session.GetComponent<AnimatorSelection>().Bone, Is.EqualTo(-1));

        Frame();
        Assert.That(VisibleBones(), Has.Count.EqualTo(3));

        Run(ToolActionKind.Redo);
        Assert.That(Package.Skeleton.Bones, Has.Count.EqualTo(4));
        Assert.That(Runtime.Pose.Bones.Length, Is.EqualTo(4));
    }

    [Test]
    public void UndoingDeleteBoneRestoresTheBoneAndItsChannels()
    {
        XafAuthoring.SetBoneProperty(Package, Idle, "head",
            XafChannelProperty.TranslationX, 0.5f, 3f);
        Select(2);
        Run(ToolActionKind.DeleteBone);
        Assert.That(Package.Skeleton.Bones, Has.Count.EqualTo(2));

        Run(ToolActionKind.Undo);

        Assert.That(Package.Skeleton.Bones, Has.Count.EqualTo(3));
        Assert.That(Idle.Channels.Exists(channel => channel.TargetId == "head"), Is.True);
        Assert.That(XafValidation.Validate(Package), Is.Empty);
    }

    [Test]
    public void ANewEditAfterUndoDropsRedo()
    {
        Select(1);
        Run(ToolActionKind.AddBone);
        Run(ToolActionKind.Undo);

        Select(1);
        Run(ToolActionKind.ToggleMovable);
        Run(ToolActionKind.Redo);

        Assert.That(Package.Skeleton.Bones, Has.Count.EqualTo(3));
        Assert.That(Session.GetComponent<AnimatorStatus>().Message, Is.EqualTo("NOTHING TO REDO"));
    }

    [Test]
    public void EditsThatChangeNothingAreNotRecorded()
    {
        Select(0);

        Run(ToolActionKind.ToggleMovable);

        Assert.That(CanUndo, Is.False);
    }

    [Test]
    public void UndoneStateIsNotAliasedByLaterEdits()
    {
        Select(1);
        Runtime.Seek(0.5f);
        Run(ToolActionKind.KeyBone);
        float first = Channel("torso", XafChannelProperty.Rotation)!.Keyframes[0].Value;

        Run(ToolActionKind.Undo);
        Run(ToolActionKind.Redo);
        Runtime.Pose.Bones[1].Rotation = 2f;
        Run(ToolActionKind.KeyBone);
        Assert.That(Channel("torso", XafChannelProperty.Rotation)!.Keyframes[0].Value, Is.EqualTo(2f));

        Run(ToolActionKind.Undo);

        Assert.That(Channel("torso", XafChannelProperty.Rotation)!.Keyframes[0].Value,
            Is.EqualTo(first));
    }

    [Test]
    public void UndoKeepsTheClipTimeAndPauseState()
    {
        Select(1);
        Run(ToolActionKind.AddBone);
        Runtime.Seek(1.25f);
        Runtime.SetPaused(true);

        Run(ToolActionKind.Undo);

        Assert.That(Runtime.CurrentClip, Is.EqualTo("idle"));
        Assert.That(Runtime.CurrentTime, Is.EqualTo(1.25f).Within(0.001f));
        Assert.That(Runtime.Paused, Is.True);
    }

    [Test]
    public void ARestPoseDragIsOneUndoStep()
    {
        Session.GetComponent<AnimatorModes>().Rest = true;
        Vector2 start = BoneScreenPosition(1);
        Pointer.Position = start;
        Pointer.Pressed = true;
        Ui.Preview.AddTag<Pressed>();
        Frame();
        foreach (float dy in new[] { 5f, 12f, 20f })
        {
            Pointer.Position = start + new Vector2(0, dy);
            Frame();
        }

        Pointer.Pressed = false;
        Ui.Preview.RemoveTag<Pressed>();
        Frame();
        Assert.That(Package.Skeleton.Bones[1].Rest.Origin.Y, Is.EqualTo(-60f).Within(0.01f));

        Run(ToolActionKind.Undo);

        Assert.That(Package.Skeleton.Bones[1].Rest.Origin.Y, Is.EqualTo(-80f).Within(0.01f));
        Assert.That(CanUndo, Is.False);
    }

    [Test]
    public void APoseOnlyDragRecordsNothing()
    {
        Vector2 start = BoneScreenPosition(1);
        Pointer.Position = start;
        Pointer.Pressed = true;
        Ui.Preview.AddTag<Pressed>();
        Frame();
        Pointer.Position = start + new Vector2(60, 0);
        Frame();
        Pointer.Pressed = false;
        Ui.Preview.RemoveTag<Pressed>();
        Frame();

        Assert.That(CanUndo, Is.False);
    }

    private static List<float> HeadKeyTimes(XafClip clip) =>
        XafTracks.KeyTimes(clip, new XafTrack(XafChannelTarget.Bone, "head"));

    [Test]
    public void RetimingAKeyByDraggingIsOneUndoStep()
    {
        Pointer.Position = KeyPoint("head", 1f);
        Pointer.Pressed = true;
        Ui.Timeline.AddTag<Pressed>();
        Frame();
        foreach (float time in new[] { 1.1f, 1.3f, 1.5f })
        {
            Pointer.Position = KeyPoint("head", time);
            Frame();
        }

        Pointer.Pressed = false;
        Ui.Timeline.RemoveTag<Pressed>();
        Frame();
        Assert.That(HeadKeyTimes(Idle), Is.EqualTo(new[] { 0f, 1.5f, 2f }).Within(0.001f));

        Run(ToolActionKind.Undo);

        Assert.That(HeadKeyTimes(Idle), Is.EqualTo(new[] { 0f, 1f, 2f }).Within(0.0001f));
        Assert.That(CanUndo, Is.False);
    }

    [Test]
    public void DeletingAKeyCanBeUndone()
    {
        ref AnimatorSelection selection = ref Session.GetComponent<AnimatorSelection>();
        selection.HasKey = true;
        selection.KeyTarget = XafChannelTarget.Bone;
        selection.KeyId = "head";
        selection.KeyTime = 1f;

        Keyboard.Press(Key.Delete);
        Frame();
        Assert.That(HeadKeyTimes(Idle), Is.EqualTo(new[] { 0f, 2f }));

        Run(ToolActionKind.Undo);

        Assert.That(HeadKeyTimes(Idle), Is.EqualTo(new[] { 0f, 1f, 2f }));
    }

    [Test]
    public void LoadingAPackageForgetsTheHistory()
    {
        Select(1);
        Run(ToolActionKind.AddBone);
        Files.ToLoad = XafRigAuthoring.CreateEmptyPackage();

        Run(ToolActionKind.Load);
        Run(ToolActionKind.Load);

        Assert.That(CanUndo, Is.False);
    }

    [Test]
    public void ControlZUndoesOnceAndControlYRedoes()
    {
        Select(1);
        Run(ToolActionKind.AddBone);

        Keyboard.Press(Key.Ctrl);
        Keyboard.Press(Key.Z);
        Frame(5);
        Assert.That(Package.Skeleton.Bones, Has.Count.EqualTo(3));

        Keyboard.Release(Key.Z);
        Keyboard.Press(Key.Y);
        Frame(5);
        Assert.That(Package.Skeleton.Bones, Has.Count.EqualTo(4));
    }

    [Test]
    public void ControlShiftZRedoes()
    {
        Select(1);
        Run(ToolActionKind.AddBone);
        Run(ToolActionKind.Undo);

        Keyboard.Press(Key.Ctrl);
        Keyboard.Press(Key.Shift);
        Keyboard.Press(Key.Z);
        Frame();

        Assert.That(Package.Skeleton.Bones, Has.Count.EqualTo(4));
    }

    [Test]
    public void ZWithoutControlDoesNothing()
    {
        Select(1);
        Run(ToolActionKind.AddBone);

        Keyboard.Press(Key.Z);
        Frame();

        Assert.That(Package.Skeleton.Bones, Has.Count.EqualTo(4));
    }

    [Test]
    public void UndoAndRedoButtonsWork()
    {
        Select(1);
        Run(ToolActionKind.AddBone);

        Click(Button(ToolActionKind.Undo));
        Assert.That(Package.Skeleton.Bones, Has.Count.EqualTo(3));

        Click(Button(ToolActionKind.Redo));
        Assert.That(Package.Skeleton.Bones, Has.Count.EqualTo(4));
    }

    [Test]
    public void UndoWithEmptyHistoryReportsItself()
    {
        Run(ToolActionKind.Undo);

        Assert.That(Session.GetComponent<AnimatorStatus>().Message, Is.EqualTo("NOTHING TO UNDO"));
    }
}

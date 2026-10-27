using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Systems.Ui;
using Xanadu.Tests.Support;
using Xanadu.Tools.Animator;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Systems;

namespace Xanadu.Tests.Animation.Tool;

/// <summary>Shift-click bone groups, and keying/nudging/resetting the whole group.</summary>
[TestFixture]
public sealed class AnimatorBoneSelectionTests : AnimatorToolTest
{
    protected override void AddSystems()
    {
        Root.Add(new UiTreeSystem());
        Root.Add(new AnimatorActionSystem());
        Root.Add(new AnimatorPreviewInputSystem(Pointer, Pointer));
        Root.Add(new AnimatorHierarchySystem(new FakeVisualRidFactory(), Pointer));
        Root.Add(new AnimatorInspectorSystem());
    }

    private AnimatorSelection Selection => Session.GetComponent<AnimatorSelection>();

    private void Select(params int[] bones)
    {
        ref AnimatorSelection selection = ref Session.GetComponent<AnimatorSelection>();
        selection.SelectOnlyBone(bones[0]);
        for (int i = 1; i < bones.Length; i++)
            selection.ToggleBone(bones[i]);
        Frame();
    }

    private void PressPreview(Vector2 position, bool shift = false)
    {
        Pointer.Position = position;
        Pointer.Pressed = true;
        Pointer.Shift = shift;
        Ui.Preview.AddTag<Pressed>();
        Frame();
    }

    private void ReleasePreview()
    {
        Pointer.Pressed = false;
        Pointer.Shift = false;
        Ui.Preview.RemoveTag<Pressed>();
        Frame();
    }

    [Test]
    public void ShiftClickingAPreviewJointAddsTheBoneItWouldTurn()
    {
        PressPreview(BoneScreenPosition(0));
        ReleasePreview();

        // The head is fixed length, so clicking it picks the torso that turns.
        PressPreview(BoneScreenPosition(2), shift: true);
        ReleasePreview();

        Assert.That(Selection.Bones(), Is.EqualTo(new[] { 0, 1 }));
    }

    [Test]
    public void APlainClickClearsTheGroup()
    {
        Select(0, 1);

        PressPreview(BoneScreenPosition(2));
        ReleasePreview();

        Assert.That(Selection.Bones(), Is.EqualTo(new[] { 1 }));
    }

    [Test]
    public void ShiftClickingASelectedBoneTakesItOutAgain()
    {
        Select(0, 1);

        Pointer.Shift = true;
        PressPreview(BoneScreenPosition(2), shift: true);
        ReleasePreview();

        Assert.That(Selection.Bones(), Is.EqualTo(new[] { 0 }));
    }

    [Test]
    public void KeyingKeysEverySelectedBone()
    {
        Select(1, 2);
        Runtime.Seek(0.5f);

        Click(Button(ToolActionKind.KeyBone));

        Assert.That(
            XafTracks.KeyTimes(Idle, new XafTrack(XafChannelTarget.Bone, "torso")),
            Does.Contain(0.5f));
        Assert.That(
            XafTracks.KeyTimes(Idle, new XafTrack(XafChannelTarget.Bone, "head")),
            Does.Contain(0.5f));
    }

    [Test]
    public void NudgingTurnsEverySelectedBone()
    {
        Select(1, 2);
        Runtime.Seek(0f);

        Click(Button(ToolActionKind.RotateRight));

        Assert.That(Runtime.Pose.Bones[1].Rotation, Is.EqualTo(AnimatorEditing.RotationStep).Within(0.0001f));
        Assert.That(Runtime.Pose.Bones[2].Rotation, Is.EqualTo(AnimatorEditing.RotationStep).Within(0.0001f));
    }

    [Test]
    public void ResettingClearsEverySelectedBonesPose()
    {
        Select(1, 2);
        Runtime.Pose.Bones[1].Rotation = 1f;
        Runtime.Pose.Bones[2].Rotation = 1f;
        Frame();

        Click(Button(ToolActionKind.ResetTransform));

        Assert.That(Runtime.Pose.Bones[1].Rotation, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(Runtime.Pose.Bones[2].Rotation, Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void TheInspectorNamesThePrimaryBoneAndCountsTheRest()
    {
        Select(1, 2);
        Frame();

        Assert.That(Ui.InspectorTitle.GetComponent<Text>().Value, Is.EqualTo("torso  +1"));
    }

    [Test]
    public void ShiftClickingATreeRowAddsItToTheGroup()
    {
        Click(BoneLabel("torso"));
        Assert.That(Selection.Bones(), Has.Count.EqualTo(1));

        Pointer.Shift = true;
        Click(BoneLabel("head"));
        Pointer.Shift = false;

        Assert.That(Selection.Bones(), Has.Count.EqualTo(2));
        Assert.That(Selection.IsBoneSelected(2), Is.True);
    }

    [Test]
    public void TheGroupIsTintedInTheBoneTree()
    {
        Select(1, 2);
        Frame();

        Entity head = BoneLabel("head");
        Assert.That(head.GetComponent<ButtonVisual>().Normal, Is.EqualTo(UiTheme.Selected));
    }
}

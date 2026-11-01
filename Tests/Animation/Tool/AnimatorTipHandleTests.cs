using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Tools.Animator;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Systems;

namespace Xanadu.Tests.Animation.Tool;

/// <summary>In the test rig "head" (bone 2) is the one childless bone.</summary>
[TestFixture]
public sealed class AnimatorTipHandleTests : AnimatorToolTest
{
    private const int Head = 2;

    protected override void AddSystems() =>
        Root.Add(new AnimatorPreviewInputSystem(Pointer));

    private void PressAt(Vector2 position)
    {
        Pointer.Position = position;
        Pointer.Pressed = true;
        Ui.Preview.AddTag<Pressed>();
        Frame();
    }

    private void MoveTo(Vector2 position)
    {
        Pointer.Position = position;
        Frame();
    }

    private void Release()
    {
        Pointer.Pressed = false;
        Ui.Preview.RemoveTag<Pressed>();
        Frame();
    }

    /// <summary>The clip's head rotation is zero at its first key, so the head starts unrotated.</summary>
    private void AtRest()
    {
        Runtime.Seek(0f);
        Frame();
    }

    private Vector2 Tip(int bone = Head)
    {
        AnimatorDocument document = Session.GetComponent<AnimatorDocument>();
        XafPose pose = AnimatorGeometry.PreviewPose(document, Session.GetComponent<AnimatorModes>());
        Transform2D[] world = XafRig.WorldBones(document.Package, pose);
        Transform2D placement = AnimatorGeometry.PreviewPlacement(
            AnimatorGeometry.PanelArea(Ui.Preview), Session.GetComponent<AnimatorPreviewView>());
        return AnimatorTipHandles.Tip(document.Package, bone, world, placement);
    }

    [Test]
    public void OnlyChildlessBonesCarryAHandle()
    {
        Assert.That(AnimatorTipHandles.Childless(Package), Is.EqualTo(new[] { false, false, true }));
    }

    [Test]
    public void TheStemContinuesTheLimbAtAFixedScreenLength()
    {
        AtRest();

        // The head sits straight above the torso, so its stem points up.
        Assert.That(Tip() - BoneScreenPosition(Head),
            Is.EqualTo(new Vector2(0, -AnimatorTipHandles.StemLength)).Using<Vector2>(Close));

        Session.GetComponent<AnimatorPreviewView>().ZoomAt(PreviewOrigin(), AnimatorGeometry.PanelArea(Ui.Preview), 2f);
        Assert.That(Tip().DistanceTo(ZoomedHead()), Is.EqualTo(AnimatorTipHandles.StemLength).Within(0.01f));
    }

    private Vector2 ZoomedHead()
    {
        Transform2D placement = AnimatorGeometry.PreviewPlacement(
            AnimatorGeometry.PanelArea(Ui.Preview), Session.GetComponent<AnimatorPreviewView>());
        return placement * XafRig.WorldBones(Package, Runtime.Pose)[Head].Origin;
    }

    [Test]
    public void GrabbingTheDotSelectsTheBoneItselfWithoutMovingIt()
    {
        AtRest();
        Runtime.SetPaused(false);

        PressAt(Tip());

        Assert.That(Session.GetComponent<AnimatorSelection>().Bone, Is.EqualTo(Head));
        Assert.That(Session.GetComponent<AnimatorDrag>().Kind, Is.EqualTo(AnimatorDragKind.Rotate));
        Assert.That(Runtime.Paused, Is.True);
        Assert.That(Runtime.Pose.Bones[Head].Rotation, Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void GrabbingTheJointStillSwingsTheParent()
    {
        PressAt(BoneScreenPosition(Head));

        Assert.That(Session.GetComponent<AnimatorSelection>().Bone, Is.EqualTo(1));
        Assert.That(Session.GetComponent<AnimatorDrag>().Kind, Is.EqualTo(AnimatorDragKind.Swing));
    }

    [Test]
    public void DraggingTurnsTheBoneAboutItsOwnJointAndNothingElse()
    {
        AtRest();
        Vector2 joint = BoneScreenPosition(Head);
        PressAt(Tip());

        Pointer.Shift = true;
        MoveTo(joint + new Vector2(100, 0));

        Assert.That(Runtime.Pose.Bones[Head].Rotation, Is.EqualTo(Mathf.Pi / 2).Within(0.001f));
        Assert.That(Runtime.Pose.Bones[1].Rotation, Is.EqualTo(0f).Within(0.001f));
        Assert.That(BoneScreenPosition(Head), Is.EqualTo(joint).Using<Vector2>(Close));
    }

    [Test]
    public void TheTurnIsRelativeToTheGrabSoAnOffCentreGrabDoesNotJump()
    {
        AtRest();
        Vector2 grab = Tip() + new Vector2(5, 0);
        PressAt(grab);

        MoveTo(grab);

        Assert.That(Runtime.Pose.Bones[Head].Rotation, Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void DraggingSnapsToStepsUnlessShiftIsHeld()
    {
        AtRest();
        Vector2 joint = BoneScreenPosition(Head);
        PressAt(Tip());

        // 20 degrees clockwise of straight up.
        Vector2 off = Vector2.Up.Rotated(Mathf.DegToRad(20f)) * 100f;
        MoveTo(joint + off);
        Assert.That(Runtime.Pose.Bones[Head].Rotation, Is.EqualTo(AnimatorTipHandles.SnapStep).Within(0.001f));

        Pointer.Shift = true;
        MoveTo(joint + off + new Vector2(0.001f, 0));
        Assert.That(Runtime.Pose.Bones[Head].Rotation, Is.EqualTo(Mathf.DegToRad(20f)).Within(0.001f));
    }

    [Test]
    public void GoingPastHalfACircleKeepsCounting()
    {
        AtRest();
        Vector2 joint = BoneScreenPosition(Head);
        PressAt(Tip());
        Pointer.Shift = true;

        foreach (float degrees in new[] { 60f, 120f, 170f, 200f })
            MoveTo(joint + Vector2.Up.Rotated(Mathf.DegToRad(degrees)) * 100f);

        Assert.That(Runtime.Pose.Bones[Head].Rotation, Is.EqualTo(Mathf.DegToRad(200f)).Within(0.001f));
    }

    [Test]
    public void AMirroredParentStillFollowsThePointer()
    {
        Package.Skeleton.Bones[1].Rest = new Transform2D(0f, new Vector2(-1, 1), 0f, new Vector2(0, -80));
        AtRest();
        Vector2 joint = BoneScreenPosition(Head);
        PressAt(Tip());
        Pointer.Shift = true;

        MoveTo(joint + new Vector2(100, 0));

        Assert.That(Tip() - joint,
            Is.EqualTo(new Vector2(AnimatorTipHandles.StemLength, 0)).Using<Vector2>(Close));
    }

    [Test]
    public void ReleasingKeysTheTurnedBoneWithAutoKey()
    {
        AtRest();
        Session.GetComponent<AnimatorModes>().AutoKey = true;
        Vector2 joint = BoneScreenPosition(Head);
        PressAt(Tip());
        Pointer.Shift = true;
        MoveTo(joint + new Vector2(100, 0));

        Release();

        Assert.That(Session.GetComponent<AnimatorDrag>().Kind, Is.EqualTo(AnimatorDragKind.None));
        XafChannel channel = Idle.Channels.Find(c =>
            c.TargetId == "head" && c.Property == XafChannelProperty.Rotation)!;
        Assert.That(channel.Keyframes.Find(key => key.Time == 0f)!.Value, Is.EqualTo(Mathf.Pi / 2).Within(0.001f));
    }

    [Test]
    public void TheDragIsOneUndoStep()
    {
        AtRest();
        Session.GetComponent<AnimatorModes>().Rest = true;
        Vector2 joint = BoneScreenPosition(Head);
        float before = Package.Skeleton.Bones[Head].Rest.Rotation;
        PressAt(Tip());
        Pointer.Shift = true;
        MoveTo(joint + new Vector2(100, 0));
        MoveTo(joint + new Vector2(0, 100));
        Release();

        AnimatorHistoryCommands.Undo(Session);

        Assert.That(Package.Skeleton.Bones[Head].Rest.Rotation, Is.EqualTo(before).Within(0.001f));
    }

    [Test]
    public void InTheRestPoseItTurnsTheRestAndKeepsTheBoneInPlace()
    {
        Session.GetComponent<AnimatorModes>().Rest = true;
        Frame();
        Vector2 joint = BoneScreenPosition(Head);
        PressAt(Tip());
        Pointer.Shift = true;

        MoveTo(joint + new Vector2(100, 0));

        XafBone head = Package.Skeleton.Bones[Head];
        Assert.That(head.Rest.Rotation, Is.EqualTo(Mathf.Pi / 2).Within(0.001f));
        Assert.That(head.Rest.Origin, Is.EqualTo(new Vector2(0, -70)).Using<Vector2>(Close));
    }

    [Test]
    public void AJointOnTheDotWins()
    {
        // "ear" hangs off the torso exactly where the head's dot ends up.
        Package.Skeleton.Bones.Add(new XafBone
        {
            Id = "ear",
            Parent = 1,
            Rest = new Transform2D(0f, new Vector2(0, -70 - AnimatorTipHandles.StemLength)),
        });
        var pose = new XafPose(4, 0);
        for (int i = 0; i < 4; i++)
            pose.Bones[i] = XafTransform.From(Package.Skeleton.Bones[i].Rest);
        Transform2D[] world = XafRig.WorldBones(Package, pose);
        Transform2D placement = AnimatorGeometry.PreviewPlacement(
            AnimatorGeometry.PanelArea(Ui.Preview), Session.GetComponent<AnimatorPreviewView>());

        Vector2 dot = AnimatorTipHandles.Tip(Package, Head, world, placement);

        Assert.That(dot, Is.EqualTo(placement * world[3].Origin).Using<Vector2>(Close));
        Assert.That(AnimatorTipHandles.Pick(Package, world, placement, dot), Is.EqualTo(-1));
    }

    [Test]
    public void HiddenHandlesCannotBeGrabbed()
    {
        AtRest();
        Vector2 tip = Tip();
        Session.GetComponent<AnimatorModes>().Handles = false;

        PressAt(tip);

        Assert.That(Session.GetComponent<AnimatorDrag>().Kind, Is.EqualTo(AnimatorDragKind.None));
    }

    [Test]
    public void SvgModeLeavesTheHandlesAlone()
    {
        AtRest();
        Vector2 tip = Tip();
        Session.GetComponent<AnimatorModes>().Svg = true;

        PressAt(tip);

        Assert.That(Session.GetComponent<AnimatorDrag>().Kind, Is.Not.EqualTo(AnimatorDragKind.Rotate));
    }

    [Test]
    public void HoveringTheDotHighlightsItsBone()
    {
        AtRest();
        MoveTo(Tip());

        Assert.That(Session.GetComponent<AnimatorHover>().Bone, Is.EqualTo(Head));
    }

    private static bool Close(Vector2 actual, Vector2 expected) =>
        actual.DistanceTo(expected) < 0.01f;
}

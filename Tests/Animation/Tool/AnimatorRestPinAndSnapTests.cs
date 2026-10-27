using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Tools.Animator;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Systems;

namespace Xanadu.Tests.Animation.Tool;

/// <summary>
/// Rest-pose edits that leave the bones below alone (PIN), and joints that
/// line up with other joints while being dragged. The rig is root (0,0), torso
/// (0,-80) and head (0,-70) below it, so the head is at (0,-150) in rest.
/// </summary>
[TestFixture]
public sealed class AnimatorRestPinAndSnapTests : AnimatorToolTest
{
    protected override void AddSystems()
    {
        Root.Add(new AnimatorActionSystem());
        Root.Add(new AnimatorPreviewInputSystem(Pointer, Pointer));
    }

    private XafBone Torso => Package.Skeleton.Bones[1];
    private XafBone Head => Package.Skeleton.Bones[2];

    private void RestMode(bool pin)
    {
        ref AnimatorModes modes = ref Session.GetComponent<AnimatorModes>();
        modes.Rest = true;
        modes.Pin = pin;
        Session.GetComponent<AnimatorSelection>().SelectOnlyBone(1);
    }

    private Vector2 HeadWorld() => AnimatorRestPin.RestWorld(Package)[2].Origin;

    /// <summary>Presses on a joint, then drags it; Shift is held only for the drag, since on the press it groups bones.</summary>
    private void DragBone(int bone, Vector2 by, bool shiftWhileDragging = false)
    {
        Vector2 start = BoneScreenPosition(bone);
        Pointer.Position = start;
        Pointer.Pressed = true;
        Ui.Preview.AddTag<Pressed>();
        Frame();
        Pointer.Shift = shiftWhileDragging;
        Pointer.Position = start + by;
        Frame();
    }

    private void ReleaseDrag()
    {
        Pointer.Pressed = false;
        Ui.Preview.RemoveTag<Pressed>();
        Frame();
    }

    [Test]
    public void WithoutPinMovingABoneCarriesItsChildren()
    {
        RestMode(pin: false);

        AnimatorTransformCommands.SetTranslation(Session, new Vector2(0, -120));

        Assert.That(HeadWorld().Y, Is.EqualTo(-190f).Within(0.001f));
    }

    [Test]
    public void WithPinMovingABoneLeavesItsChildrenWhereTheyWere()
    {
        RestMode(pin: true);

        AnimatorTransformCommands.SetTranslation(Session, new Vector2(0, -120));

        Assert.That(Torso.Rest.Origin, Is.EqualTo(new Vector2(0, -120)));
        Assert.That(HeadWorld().DistanceTo(new Vector2(0, -150)), Is.LessThan(0.001f));
        // The head's own offset took up the difference.
        Assert.That(Head.Rest.Origin.Y, Is.EqualTo(-30f).Within(0.001f));
    }

    [Test]
    public void WithPinTurningABoneLeavesItsChildrenWhereTheyWere()
    {
        RestMode(pin: true);

        AnimatorTransformCommands.SetRotation(Session, Mathf.Pi / 2f);

        Assert.That(Torso.Rest.Rotation, Is.EqualTo(Mathf.Pi / 2f).Within(0.0001f));
        Assert.That(HeadWorld().DistanceTo(new Vector2(0, -150)), Is.LessThan(0.001f));
    }

    [Test]
    public void WithPinScalingAndNudgingKeepTheChildrenToo()
    {
        RestMode(pin: true);

        AnimatorTransformCommands.SetScale(Session, new Vector2(2f, 2f));
        Assert.That(HeadWorld().DistanceTo(new Vector2(0, -150)), Is.LessThan(0.001f));

        AnimatorEditing.Nudge(Session, 0.3f, 0f);
        Assert.That(HeadWorld().DistanceTo(new Vector2(0, -150)), Is.LessThan(0.001f));
    }

    [Test]
    public void PinningReachesDeeperThanTheFirstChild()
    {
        // Add a hand under the head; moving the torso must leave it put as well.
        Package.Skeleton.Bones.Add(new XafBone { Id = "hand", Parent = 2, Rest = new Transform2D(0f, new Vector2(30, 0)) });
        AnimatorSession.RigChanged(Session);
        RestMode(pin: true);
        Vector2 before = AnimatorRestPin.RestWorld(Package)[3].Origin;

        AnimatorTransformCommands.SetTranslation(Session, new Vector2(40, -120));

        Assert.That(AnimatorRestPin.RestWorld(Package)[3].Origin.DistanceTo(before), Is.LessThan(0.001f));
    }

    [Test]
    public void PinDoesNothingOutsideTheRestPose()
    {
        RestMode(pin: true);
        Session.GetComponent<AnimatorModes>().Rest = false;

        Assert.That(AnimatorRestPin.IsOn(Session), Is.False);
        Assert.That(AnimatorRestPin.Capture(Session, new[] { 1 }), Is.Null);
    }

    [Test]
    public void DraggingABoneWithPinLeavesItsChildrenAndIsOneUndoStep()
    {
        RestMode(pin: true);

        DragBone(1, new Vector2(60, 0));
        ReleaseDrag();

        Assert.That(Torso.Rest.Origin.X, Is.EqualTo(60f).Within(0.5f));
        Assert.That(HeadWorld().DistanceTo(new Vector2(0, -150)), Is.LessThan(0.01f));

        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.Undo));
        Assert.That(Torso.Rest.Origin, Is.EqualTo(new Vector2(0, -80)));
        Assert.That(HeadWorld().DistanceTo(new Vector2(0, -150)), Is.LessThan(0.001f));
    }

    [Test]
    public void ADraggedJointSnapsItsXToAnotherJointAndShowsAGuide()
    {
        // Head at x 0; dragging the torso 5 px right is within snapping range of the root and head.
        RestMode(pin: false);

        DragBone(1, new Vector2(5, 0));

        Assert.That(Torso.Rest.Origin.X, Is.EqualTo(0f).Within(0.0001f));
        AnimatorSnapGuides guides = Session.GetComponent<AnimatorSnapGuides>();
        Assert.That(guides.HasX, Is.True);
        Assert.That(guides.X, Is.EqualTo(0f).Within(0.0001f));

        ReleaseDrag();
        Assert.That(Session.GetComponent<AnimatorSnapGuides>().HasX, Is.False);
    }

    [Test]
    public void ADraggedJointSnapsItsYToo()
    {
        // Drag the head (at y -150) down to within range of the torso's -80.
        RestMode(pin: false);

        DragBone(2, new Vector2(30, 73));

        AnimatorSnapGuides guides = Session.GetComponent<AnimatorSnapGuides>();
        Assert.That(guides.HasY, Is.True);
        Assert.That(guides.Y, Is.EqualTo(-80f).Within(0.0001f));
    }

    [Test]
    public void HoldingShiftDragsFreelyWithNoGuides()
    {
        RestMode(pin: false);

        DragBone(1, new Vector2(5, 0), shiftWhileDragging: true);

        Assert.That(Torso.Rest.Origin.X, Is.EqualTo(5f).Within(0.5f));
        Assert.That(Session.GetComponent<AnimatorSnapGuides>().HasX, Is.False);
    }

    [Test]
    public void ABoneNeverSnapsToItsOwnChildren()
    {
        // The head moves with the torso, so lining the torso up with it means nothing.
        // This puts the torso at y -146, four units from the head's -150.
        RestMode(pin: false);

        DragBone(1, new Vector2(40, -66));

        Assert.That(Session.GetComponent<AnimatorSnapGuides>().HasY, Is.False);
        Assert.That(Torso.Rest.Origin.Y, Is.EqualTo(-146f).Within(0.5f));
        Assert.That(AnimatorSnap.Subtree(Package, 1), Is.EquivalentTo(new[] { 1, 2 }));
    }

    [Test]
    public void SwingingABoneTurnsItIntoLineWithAnotherJoint()
    {
        // Pose mode: the head is fixed length, so dragging it swings the torso. The head
        // sits straight above the torso and the root; drag it a little off and it snaps back.
        DragBone(2, new Vector2(5, 0));

        Assert.That(Session.GetComponent<AnimatorDrag>().Kind, Is.EqualTo(AnimatorDragKind.Swing));
        Assert.That(Runtime.Pose.Bones[1].Rotation, Is.EqualTo(0f).Within(0.0001f));
        AnimatorSnapGuides guides = Session.GetComponent<AnimatorSnapGuides>();
        Assert.That(guides.HasX, Is.True);
        Assert.That(guides.X, Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void SwingingWithShiftHeldTurnsFreely()
    {
        DragBone(2, new Vector2(5, 0), shiftWhileDragging: true);

        Assert.That(Mathf.Abs(Runtime.Pose.Bones[1].Rotation), Is.GreaterThan(0.01f));
        Assert.That(Session.GetComponent<AnimatorSnapGuides>().HasX, Is.False);
    }

    [Test]
    public void SwingingKeepsTheBonesLength()
    {
        DragBone(2, new Vector2(5, 0));

        Transform2D[] world = XafRig.WorldBones(Package, Runtime.Pose);
        Assert.That(world[2].Origin.DistanceTo(world[1].Origin), Is.EqualTo(70f).Within(0.01f));
    }

    [Test]
    public void SwingingSnapsToAJointOnTheOtherAxisToo()
    {
        // A marker at the head's height to the right; swing the head toward it.
        Package.Skeleton.Bones.Add(new XafBone { Id = "marker", Parent = -1, Rest = new Transform2D(0f, new Vector2(60, -75)) });
        AnimatorSession.RigChanged(Session);

        // Pointing right of the torso, near the marker's Y (-75): the torso is at -80, so the head ends up level with it.
        DragBone(2, new Vector2(70, 73));

        AnimatorSnapGuides guides = Session.GetComponent<AnimatorSnapGuides>();
        Transform2D[] world = XafRig.WorldBones(Package, Runtime.Pose);
        Assert.That(guides.HasY || guides.HasX, Is.True);
        Assert.That(world[2].Origin.DistanceTo(world[1].Origin), Is.EqualTo(70f).Within(0.01f));
        if (guides.HasY)
            Assert.That(world[2].Origin.Y, Is.EqualTo(guides.Y).Within(0.01f));
    }

    [Test]
    public void AnIkTargetSnapsToAJointOutsideTheChain()
    {
        // A separate root bone to snap to; the chain is root > torso > head.
        Package.Skeleton.Bones.Add(new XafBone { Id = "marker", Parent = -1, Rest = new Transform2D(0f, new Vector2(30, -100)) });
        AnimatorSession.RigChanged(Session);
        Session.GetComponent<AnimatorModes>().Ik = true;

        DragBone(2, new Vector2(27, 50));

        Transform2D[] world = XafRig.WorldBones(Package, Runtime.Pose);
        Assert.That(world[2].Origin.X, Is.EqualTo(30f).Within(0.01f));
        Assert.That(Session.GetComponent<AnimatorSnapGuides>().HasX, Is.True);
    }
}

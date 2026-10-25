using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Writer.Animation;
using Writer.Ecs;
using Writer.Tools.Animator;
using Writer.Tools.Animator.Systems;

namespace Writer.Tests.Animation.Tool;

[TestFixture]
public sealed class AnimatorPreviewInputTests : AnimatorToolTest
{
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

    [Test]
    public void GrabbingAFixedLengthJointSelectsItsParentAndPausesPlayback()
    {
        Runtime.SetPaused(false);

        PressAt(BoneScreenPosition(1));

        Assert.That(Session.GetComponent<AnimatorSelection>().Bone, Is.EqualTo(0));
        Assert.That(Session.GetComponent<AnimatorDrag>().Kind, Is.EqualTo(AnimatorDragKind.Swing));
        Assert.That(Runtime.Paused, Is.True);
    }

    [Test]
    public void SwingingTurnsTheParentTowardThePointerWithoutChangingLength()
    {
        PressAt(BoneScreenPosition(1));

        // The torso joint sits straight above the root; pull it to the right.
        MoveTo(PreviewOrigin() + new Vector2(200, 0));

        Transform2D[] world = XafRig.WorldBones(Package, Runtime.Pose);
        Assert.That(world[1].Origin,
            Is.EqualTo(new Vector2(80, 0)).Using<Vector2>(Close));
        Assert.That(Runtime.Pose.Bones[0].Rotation, Is.EqualTo(Mathf.Pi / 2).Within(0.001f));
        Assert.That(Runtime.Pose.Bones[1].Translation,
            Is.EqualTo(new Vector2(0, -80)).Using<Vector2>(Close));
    }

    [Test]
    public void SwingingKeepsTheBoneLengthWhereverThePointerGoes()
    {
        PressAt(BoneScreenPosition(1));

        foreach (Vector2 offset in new[] { new Vector2(-300, 40), new Vector2(5, 5), new Vector2(0, 500) })
        {
            MoveTo(PreviewOrigin() + offset);

            Transform2D[] world = XafRig.WorldBones(Package, Runtime.Pose);
            Assert.That(world[1].Origin.DistanceTo(world[0].Origin), Is.EqualTo(80f).Within(0.01f));
        }
    }

    [Test]
    public void SwingingFollowsAnAlreadyRotatedParent()
    {
        Package.Skeleton.Bones[0].Rest = new Transform2D(1f, Vector2.Zero);
        Runtime.Seek(0f);
        PressAt(BoneScreenPosition(1));

        MoveTo(PreviewOrigin() + new Vector2(-100, 0));

        Transform2D[] world = XafRig.WorldBones(Package, Runtime.Pose);
        Vector2 direction = world[1].Origin.Normalized();
        Assert.That(direction, Is.EqualTo(new Vector2(-1, 0)).Using<Vector2>(Close));
    }

    [Test]
    public void DraggingTheRootTranslatesIt()
    {
        Vector2 start = BoneScreenPosition(0);
        PressAt(start);

        MoveTo(start + new Vector2(10, 5));

        Assert.That(Session.GetComponent<AnimatorDrag>().Kind, Is.EqualTo(AnimatorDragKind.Bone));
        Assert.That(Runtime.Pose.Bones[0].Translation,
            Is.EqualTo(new Vector2(10, 5)).Using<Vector2>(Close));
    }

    [Test]
    public void PressingEmptySpaceSelectsNothing()
    {
        PressAt(PreviewOrigin() + new Vector2(300, 200));

        Assert.That(Session.GetComponent<AnimatorSelection>().Bone, Is.EqualTo(-1));
        Assert.That(Session.GetComponent<AnimatorDrag>().Kind, Is.EqualTo(AnimatorDragKind.None));
    }

    [Test]
    public void DraggingAMovableBoneTranslatesIt()
    {
        Package.Skeleton.Bones[1].AllowTranslation = true;
        Vector2 start = BoneScreenPosition(1);
        PressAt(start);

        MoveTo(start + new Vector2(10, 5));

        Assert.That(Session.GetComponent<AnimatorSelection>().Bone, Is.EqualTo(1));
        Assert.That(Runtime.Pose.Bones[1].Translation,
            Is.EqualTo(new Vector2(10, -75)).Using<Vector2>(Close));
        Release();
        Assert.That(Session.GetComponent<AnimatorDrag>().Kind, Is.EqualTo(AnimatorDragKind.None));
    }

    [Test]
    public void DraggingFollowsTheParentsRotation()
    {
        // Root rotated a quarter turn: screen-right is local -Y.
        Package.Skeleton.Bones[1].AllowTranslation = true;
        Package.Skeleton.Bones[0].Rest = new Transform2D(Mathf.Pi / 2, Vector2.Zero);
        Runtime.Seek(0f);
        Vector2 start = BoneScreenPosition(1);
        PressAt(start);

        MoveTo(start + new Vector2(10, 0));

        Assert.That(Runtime.Pose.Bones[1].Translation,
            Is.EqualTo(new Vector2(0, -90)).Using<Vector2>(Close));
    }

    [Test]
    public void DraggingInRestModeEditsTheRestPoseOnly()
    {
        Session.GetComponent<AnimatorModes>().Rest = true;
        Vector2 start = BoneScreenPosition(1);
        PressAt(start);

        MoveTo(start + new Vector2(0, 20));

        Assert.That(Package.Skeleton.Bones[1].Rest.Origin,
            Is.EqualTo(new Vector2(0, -60)).Using<Vector2>(Close));
        Assert.That(Runtime.Pose.Bones[1].Translation,
            Is.EqualTo(new Vector2(0, -80)).Using<Vector2>(Close));
    }

    [Test]
    public void ReleasingWithAutoKeyKeysTheRotatedBoneAndNoTranslation()
    {
        Session.GetComponent<AnimatorModes>().AutoKey = true;
        PressAt(BoneScreenPosition(1));
        MoveTo(PreviewOrigin() + new Vector2(200, 0));

        Release();

        XafChannel? rotation = Idle.Channels.Find(channel =>
            channel.TargetId == "root" &&
            channel.Property == XafChannelProperty.Rotation);
        Assert.That(rotation, Is.Not.Null);
        Assert.That(rotation!.Keyframes[0].Value, Is.EqualTo(Mathf.Pi / 2).Within(0.001f));
        Assert.That(Idle.Channels.Exists(channel =>
            channel.TargetId == "torso" &&
            XafSampler.IsTranslation(channel.Property)), Is.False);
    }

    [Test]
    public void ReleasingAMovableBoneWithAutoKeyKeysItsTranslation()
    {
        Package.Skeleton.Bones[1].AllowTranslation = true;
        Session.GetComponent<AnimatorModes>().AutoKey = true;
        Vector2 start = BoneScreenPosition(1);
        PressAt(start);
        MoveTo(start + new Vector2(10, 0));

        Release();

        XafChannel? x = Idle.Channels.Find(channel =>
            channel.TargetId == "torso" &&
            channel.Property == XafChannelProperty.TranslationX);
        Assert.That(x, Is.Not.Null);
        Assert.That(x!.Keyframes[0].Value, Is.EqualTo(10f).Within(0.001f));
    }

    [Test]
    public void ReleasingWithoutAutoKeyKeysNothing()
    {
        int channels = Idle.Channels.Count;
        Vector2 start = BoneScreenPosition(1);
        PressAt(start);
        MoveTo(start + new Vector2(10, 0));

        Release();

        Assert.That(Idle.Channels, Has.Count.EqualTo(channels));
    }

    [Test]
    public void PressingAnAttachmentMarkerSelectsAndDragsIt()
    {
        AnimatorDocument document = Session.GetComponent<AnimatorDocument>();
        XafPose pose = document.Runtime.Pose;
        Transform2D[] bones = XafRig.WorldBones(Package, pose);
        Vector2 marker = PreviewOrigin() +
                         XafRig.WorldAttachments(Package, pose, bones)[0].Origin;
        PressAt(marker);

        MoveTo(marker + new Vector2(0, 8));

        Assert.That(Session.GetComponent<AnimatorSelection>().Attachment, Is.EqualTo(0));
        Assert.That(Runtime.Pose.Attachments[0].Translation,
            Is.EqualTo(new Vector2(0, -12)).Using<Vector2>(Close));
    }

    [Test]
    public void PressesOutsideThePreviewPanelAreIgnored()
    {
        Pointer.Position = BoneScreenPosition(1);
        Pointer.Pressed = true;
        Frame();

        Assert.That(Session.GetComponent<AnimatorDrag>().Kind, Is.EqualTo(AnimatorDragKind.None));
    }

    private static bool Close(Vector2 actual, Vector2 expected) =>
        actual.DistanceTo(expected) < 0.01f;
}

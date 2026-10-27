using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Tools.Animator;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Systems;

namespace Xanadu.Tests.Animation.Tool;

/// <summary>The floating transform inspector: exact values, degrees, reset and the unkeyed flag.</summary>
[TestFixture]
public sealed class AnimatorInspectorTests : AnimatorToolTest
{
    protected override void AddSystems()
    {
        Root.Add(new AnimatorActionSystem());
        Root.Add(new AnimatorInspectorSystem());
    }

    private AnimatorSelection Selection => Session.GetComponent<AnimatorSelection>();

    private string FieldValue(Entity field) => field.GetComponent<TextField>().Value;

    private string Title => Ui.InspectorTitle.GetComponent<Text>().Value;

    private static bool Close(Vector2 actual, Vector2 expected) =>
        actual.DistanceTo(expected) < 0.001f;

    private void SelectBone(int bone)
    {
        ref AnimatorSelection selection = ref Session.GetComponent<AnimatorSelection>();
        selection.Bone = bone;
        Frame();
    }

    private void Type(Entity field, string value)
    {
        field.GetComponent<TextField>().Value = value;
        field.AddTag<TextFieldFocused>();
        Frame();
        field.RemoveTag<TextFieldFocused>();
        Frame();
    }

    [Test]
    public void LayerShowsAndSetsTheDrawOrderOfTheSelectedBonesArt()
    {
        // The fixture has one SVG, "body", on the torso at layer 0.
        SelectBone(1);
        Assert.That(FieldValue(Ui.InspectLayer), Is.EqualTo("0"));

        Type(Ui.InspectLayer, "-3");

        Assert.That(Package.Attachments[0].ZOrder, Is.EqualTo(-3));
        Assert.That(FieldValue(Ui.InspectLayer), Is.EqualTo("-3"));
        Assert.That(Session.GetComponent<AnimatorStatus>().Message, Does.Contain("LAYER -3"));
    }

    [Test]
    public void LayerOnABoneWithoutArtDoesNothingAndSaysSo()
    {
        SelectBone(0);
        Assert.That(FieldValue(Ui.InspectLayer), Is.EqualTo(""));

        Type(Ui.InspectLayer, "5");

        Assert.That(Package.Attachments[0].ZOrder, Is.EqualTo(0));
        Assert.That(Session.GetComponent<AnimatorStatus>().Message, Does.Contain("SELECT A BONE OR SVG"));
    }

    [Test]
    public void LayerOnASelectedSvgSetsJustThatOne()
    {
        Package.Attachments.Add(new XafAttachment
        {
            SvgPath = "res://leg.svg",
            Group = "leg",
            BoneId = "torso",
        });
        AnimatorSession.RigChanged(Session);
        Session.GetComponent<AnimatorSelection>().SelectOnlyAttachment(1);
        Frame();

        Type(Ui.InspectLayer, "-1");

        Assert.That(Package.Attachments[1].ZOrder, Is.EqualTo(-1));
        Assert.That(Package.Attachments[0].ZOrder, Is.EqualTo(0));
    }

    [Test]
    public void ABoneWhoseArtSitsOnDifferentLayersShowsNoSingleLayer()
    {
        Package.Attachments.Add(new XafAttachment
        {
            SvgPath = "res://leg.svg",
            Group = "leg",
            BoneId = "torso",
            ZOrder = 4,
        });
        AnimatorSession.RigChanged(Session);
        SelectBone(1);

        Assert.That(FieldValue(Ui.InspectLayer), Is.EqualTo(""));

        Type(Ui.InspectLayer, "2");

        Assert.That(Package.Attachments[0].ZOrder, Is.EqualTo(2));
        Assert.That(Package.Attachments[1].ZOrder, Is.EqualTo(2));
    }

    [Test]
    public void ALayerChangeIsOneUndoStep()
    {
        SelectBone(1);
        Type(Ui.InspectLayer, "7");

        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.Undo));

        Assert.That(Package.Attachments[0].ZOrder, Is.EqualTo(0));
    }

    [Test]
    public void ShowsTheSelectedBonesTransform()
    {
        SelectBone(1);

        Assert.That(FieldValue(Ui.InspectPosX), Is.EqualTo("0"));
        Assert.That(FieldValue(Ui.InspectPosY), Is.EqualTo("-80"));
        Assert.That(FieldValue(Ui.InspectRotation), Is.EqualTo("0"));
        Assert.That(FieldValue(Ui.InspectScaleX), Is.EqualTo("1"));
        Assert.That(FieldValue(Ui.InspectScaleY), Is.EqualTo("1"));
        Assert.That(Title, Is.EqualTo("torso"));
    }

    [Test]
    public void RotationIsTypedInDegreesAndShownInDegrees()
    {
        SelectBone(1);

        Type(Ui.InspectRotation, "90");

        Assert.That(Runtime.Pose.Bones[1].Rotation, Is.EqualTo(Mathf.Pi / 2).Within(0.0001f));
        Assert.That(FieldValue(Ui.InspectRotation), Is.EqualTo("90"));
    }

    [Test]
    public void TypingWithAutoKeyOnKeysTheBoneAtThePlayhead()
    {
        Session.GetComponent<AnimatorModes>().AutoKey = true;
        SelectBone(1);
        Runtime.Seek(1f);

        Type(Ui.InspectRotation, "45");

        var track = new XafTrack(XafChannelTarget.Bone, "torso");
        Assert.That(XafTracks.KeyTimes(Idle, track), Does.Contain(1f));
    }

    [Test]
    public void TypingAPositionMovesTheRoot()
    {
        SelectBone(0);

        Type(Ui.InspectPosX, "12");
        Type(Ui.InspectPosY, "-3.5");

        Assert.That(
            Runtime.Pose.Bones[0].Translation,
            Is.EqualTo(new Vector2(12, -3.5f)).Using<Vector2>(Close));
    }

    [Test]
    public void AFixedLengthBonesPositionIsRefused()
    {
        SelectBone(2);

        Type(Ui.InspectPosX, "50");

        Assert.That(Runtime.Pose.Bones[2].Translation.X, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(Session.GetComponent<AnimatorStatus>().Message, Does.Contain("FIXED"));
    }

    [Test]
    public void ResetPutsThePoseBackOnTheRestTransform()
    {
        SelectBone(1);
        Runtime.Pose.Bones[1].Rotation = 0.75f;
        Frame();

        Click(Button(ToolActionKind.ResetTransform));

        Assert.That(Runtime.Pose.Bones[1].Rotation, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(Runtime.Pose.Bones[1].Translation.Y, Is.EqualTo(-80f).Within(0.0001f));
    }

    [Test]
    public void AnUnkeyedPoseIsFlaggedInTheTitle()
    {
        SelectBone(1);
        Runtime.Pose.Bones[1].Rotation = 0.5f;
        Frame();

        Assert.That(Title, Does.Contain("UNKEYED"));
    }

    [Test]
    public void RestModeEditsTheRestTransform()
    {
        Session.GetComponent<AnimatorModes>().Rest = true;
        SelectBone(1);

        Type(Ui.InspectRotation, "30");

        Assert.That(
            Package.Skeleton.Bones[1].Rest.Rotation,
            Is.EqualTo(Mathf.DegToRad(30f)).Within(0.0001f));
        Assert.That(Title, Does.Contain("REST"));
    }

    [Test]
    public void TheSelectedAttachmentShowsAndAcceptsItsTransform()
    {
        ref AnimatorSelection selection = ref Session.GetComponent<AnimatorSelection>();
        selection.Bone = -1;
        selection.Attachment = 0;
        Frame();

        Assert.That(Title, Does.Contain("body"));
        Assert.That(Title, Does.Contain("SVG"));
        Assert.That(FieldValue(Ui.InspectPosY), Is.EqualTo("-20"));

        Type(Ui.InspectPosX, "7");

        Assert.That(Runtime.Pose.Attachments[0].Translation.X, Is.EqualTo(7f).Within(0.0001f));
    }

    [Test]
    public void AScaledBoneShowsBothComponents()
    {
        SelectBone(1);

        Type(Ui.InspectScaleX, "2");
        Type(Ui.InspectScaleY, "0.5");

        Assert.That(
            Runtime.Pose.Bones[1].Scale,
            Is.EqualTo(new Vector2(2, 0.5f)).Using<Vector2>(Close));
    }
}

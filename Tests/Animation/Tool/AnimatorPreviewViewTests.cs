using Godot;
using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Tools.Animator;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Systems;

namespace Xanadu.Tests.Animation.Tool;

/// <summary>Zooming, panning and framing the preview, and picking inside a zoomed view.</summary>
[TestFixture]
public sealed class AnimatorPreviewViewTests : AnimatorToolTest
{
    protected override void AddSystems() =>
        Root.Add(new AnimatorPreviewInputSystem(Pointer, Pointer));

    private AnimatorPreviewView View => Session.GetComponent<AnimatorPreviewView>();

    private Rect2 PreviewArea() => AnimatorGeometry.PanelArea(Ui.Preview);

    private Transform2D Placement() =>
        AnimatorGeometry.PreviewPlacement(PreviewArea(), View);

    private static bool Close(Vector2 actual, Vector2 expected) =>
        actual.DistanceTo(expected) < 0.001f;

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
    public void TheWheelZoomsAroundThePointer()
    {
        Vector2 point = PreviewOrigin() + new Vector2(120, -40);
        Vector2 local = View.ToLocal(point, PreviewArea());

        Pointer.Position = point;
        Pointer.Wheel = 2;
        Frame();

        Assert.That(View.Zoom, Is.GreaterThan(1f));
        Assert.That(
            View.ToLocal(point, PreviewArea()).DistanceTo(local),
            Is.LessThan(0.01f));
    }

    [Test]
    public void MiddleDraggingPansTheView()
    {
        Vector2 start = PreviewOrigin();

        Pointer.Position = start;
        Pointer.Middle = true;
        Frame();
        MoveTo(start + new Vector2(30, 20));

        Assert.That(View.Pan, Is.EqualTo(new Vector2(30, 20)).Using<Vector2>(Close));
    }

    [Test]
    public void FitFramesTheRigInsideThePreview()
    {
        Assert.That(AnimatorPreviewCommands.Fit(Session), Is.True);
        Frame();

        Assert.That(View.Zoom, Is.GreaterThan(1f));
        Transform2D placement = Placement();
        Transform2D[] world = XafRig.WorldBones(Package, Runtime.Pose);
        foreach (Transform2D bone in world)
            Assert.That(PreviewArea().Grow(-1f).HasPoint(placement * bone.Origin), Is.True);
    }

    [Test]
    public void PickingInAZoomedViewUsesTheZoomedPositions()
    {
        Assert.That(AnimatorPreviewCommands.Fit(Session), Is.True);
        Frame();

        Transform2D[] world = XafRig.WorldBones(Package, Runtime.Pose);
        PressAt(Placement() * world[2].Origin);

        // The head is fixed length, so grabbing it selects the torso it swings.
        Assert.That(Session.GetComponent<AnimatorSelection>().Bone, Is.EqualTo(1));
    }

    [Test]
    public void DraggingAZoomedBoneMovesItInSkeletonUnits()
    {
        Assert.That(AnimatorPreviewCommands.Fit(Session), Is.True);
        Frame();
        float zoom = View.Zoom;
        Vector2 start = Placement() * Vector2.Zero;

        PressAt(start);
        MoveTo(start + new Vector2(40, 20));

        Assert.That(
            Runtime.Pose.Bones[0].Translation,
            Is.EqualTo(new Vector2(40f / zoom, 20f / zoom)).Using<Vector2>(Close));
    }

    [Test]
    public void ResettingTheViewGoesBackToOneToOne()
    {
        AnimatorPreviewCommands.Fit(Session);
        AnimatorPreviewCommands.Reset(Session);

        Assert.That(View.Zoom, Is.EqualTo(1f));
        Assert.That(View.Pan, Is.EqualTo(Vector2.Zero));
    }
}

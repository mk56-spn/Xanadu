using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Tools.Animator;
using Xanadu.Tools.Animator.Systems;

namespace Xanadu.Tests.Animation.Tool;

public sealed class AnimatorGridSnapTests : AnimatorToolTest
{
    protected override void AddSystems() => Root.Add(new AnimatorPreviewInputSystem(Pointer));

    private Entity SnapButton
    {
        get
        {
            foreach (Entity entity in Store.Query<Toggle>().AllTags(Tags.Get<ViewportSnapToggle>()).Entities)
                return entity;
            Assert.Fail("no snap button");
            return default;
        }
    }

    private void SetSnap(bool on) => SnapButton.GetComponent<Toggle>().Value = on;

    private Vector2 Screen(Vector2 rig) =>
        AnimatorGeometry.PreviewPlacement(AnimatorGeometry.PanelArea(Ui.Preview), Session.GetComponent<AnimatorPreviewView>()) * rig;

    /// <summary>Drags the root, whose position is its rig-space position, from the origin to a spot well clear of other joints.</summary>
    private Vector2 DragRootTo(Vector2 rig, bool shift = false)
    {
        Session.GetComponent<AnimatorModes>().Rest = true;
        Pointer.Position = Screen(Vector2.Zero);
        Pointer.Pressed = true;
        Ui.Preview.AddTag<Pressed>();
        Frame();

        // Held after the press: Shift at the press itself adds to the selection instead.
        Pointer.Shift = shift;
        Pointer.Position = Screen(rig);
        Frame();
        Pointer.Pressed = false;
        Pointer.Shift = false;
        Ui.Preview.RemoveTag<Pressed>();
        Frame();
        return Package.Skeleton.Bones[0].Rest.Origin;
    }

    [Test]
    public void TheAnimatorStartsWithSnapOffSoPosingIsUnchanged() =>
        Assert.That(ViewportSnap.IsOn(Store), Is.False);

    [Test]
    public void WithSnapOnADraggedJointLandsOnTheDrawnGrid()
    {
        SetSnap(true);
        Assert.That(DragRootTo(new Vector2(127f, 53f)), Is.EqualTo(new Vector2(120f, 60f)));
    }

    [Test]
    public void WithSnapOffItStaysWhereItWasDropped()
    {
        SetSnap(false);
        Assert.That(DragRootTo(new Vector2(127f, 53f)).DistanceTo(new Vector2(127f, 53f)), Is.LessThan(0.01f));
    }

    [Test]
    public void ShiftDragsFreelyWithSnapOn()
    {
        SetSnap(true);
        Assert.That(DragRootTo(new Vector2(127f, 53f), shift: true).DistanceTo(new Vector2(127f, 53f)), Is.LessThan(0.01f));
    }

    [Test]
    public void AnAxisLinedUpWithAJointKeepsTheJoint()
    {
        Vector2 snapped = AnimatorSnap_ToGrid(new Vector2(7f, 53f), new AnimatorSnapGuides { HasX = true, X = 7f });
        Assert.That(snapped, Is.EqualTo(new Vector2(7f, 60f)));
    }

    private static Vector2 AnimatorSnap_ToGrid(Vector2 wanted, AnimatorSnapGuides guides) =>
        Xanadu.Tools.Animator.Commands.AnimatorSnap.ToGrid(wanted, guides);

    [Test]
    public void TheGridIsTheOneDrawn() =>
        Assert.That(AnimatorGridDrawSystem.Cell, Is.EqualTo(20f));
}

public sealed class ViewportSnapTests
{
    [Test]
    public void TheSumRoundsHalvesUp()
    {
        Assert.That(ViewportSnap.Snap(44f, 50f), Is.EqualTo(50f));
        Assert.That(ViewportSnap.Snap(25f, 50f), Is.EqualTo(50f));
        Assert.That(ViewportSnap.Snap(20f, 50f), Is.EqualTo(0f));
        Assert.That(ViewportSnap.Snap(-26f, 50f), Is.EqualTo(-50f));
    }

    [Test]
    public void ANoCellLeavesTheValueAlone() =>
        Assert.That(ViewportSnap.Snap(44f, 0f), Is.EqualTo(44f));

    [Test]
    public void WithNoButtonThingsSnap() =>
        Assert.That(ViewportSnap.IsOn(new EntityStore()), Is.True);
}

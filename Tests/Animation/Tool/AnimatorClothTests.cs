using Godot;
using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Tests.Support;
using Xanadu.Tools.Animator;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Systems;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.Animation.Tool;

/// <summary>
/// Cloth in the animator: adding a dress from the rig list, its inspector,
/// pinning points and choosing colliders by clicking, and undoing all of it.
/// The fixture's torso hangs 80 above the root with the head on top of it.
/// </summary>
[TestFixture]
public sealed class AnimatorClothTests : AnimatorToolTest
{
    private const int Torso = 1;

    protected override void AddSystems()
    {
        Root.Add(new AnimatorActionSystem());
        Root.Add(new AnimatorHierarchySystem(new FakeVisualRidFactory()));
        Root.Add(new AnimatorChainSimulationSystem());
        Root.Add(new AnimatorClothInspectorSystem());
    }

    private XafCloth Dress => Package.Cloths[0];

    private void AddDress()
    {
        Session.GetComponent<AnimatorSelection>().SelectOnlyBone(Torso);
        Click(Button(ToolActionKind.AddCloth));
        Frame(2);
    }

    private Transform2D Placement() =>
        AnimatorGeometry.PreviewPlacement(AnimatorGeometry.PanelArea(Ui.Preview),
            Session.GetComponent<AnimatorPreviewView>());

    private Transform2D[] World() => XafRig.WorldBones(Package, Runtime.Pose);

    [Test]
    public void TheDressButtonAddsADressOnTheSelectedBoneAndSelectsIt()
    {
        AddDress();

        Assert.That(Package.Cloths, Has.Count.EqualTo(1));
        Assert.That(Dress.BoneId, Is.EqualTo("torso"));
        Assert.That(AnimatorClothCommands.Selected(Session), Is.EqualTo(0));
        Assert.That(Ui.ClothInspector.GetComponent<Position>().Value, Is.Not.EqualTo(UiLayout.Hidden));
        Assert.That(Ui.ClothTitle.GetComponent<Text>().Value, Is.EqualTo("DRESS  CLOTH"));
    }

    [Test]
    public void WithoutABoneThereIsNoDress()
    {
        Session.GetComponent<AnimatorSelection>().SelectOnlyBone(-1);

        Click(Button(ToolActionKind.AddCloth));

        Assert.That(Package.Cloths, Is.Empty);
    }

    [Test]
    public void ClickingAPointWhilePinningPinsItToTheNearestBoneAndAgainFreesIt()
    {
        AddDress();
        int hem = Dress.Index(0, Dress.Rows - 1);
        Assert.That(Dress.IsPinned(0, Dress.Rows - 1), Is.False);
        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.PinClothPoints));
        Vector2 at = Placement() * Session.GetComponent<AnimatorCloths>().Simulation.Points(0)[hem];

        AnimatorClothCommands.Click(Session, at, Placement(), World());

        Assert.That(Dress.IsPinned(0, Dress.Rows - 1), Is.True);
        Assert.That(Session.GetComponent<AnimatorCloths>().Picking, Is.EqualTo(AnimatorClothPick.Pins),
            "pinning stays on for the next point");

        AnimatorClothCommands.Click(Session, at, Placement(), World());
        Assert.That(Dress.IsPinned(0, Dress.Rows - 1), Is.False);
    }

    [Test]
    public void ClickingNothingEndsPinning()
    {
        AddDress();
        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.PinClothPoints));

        AnimatorClothCommands.Click(Session, new Vector2(-5000, -5000), Placement(), World());

        Assert.That(Session.GetComponent<AnimatorCloths>().Picking, Is.EqualTo(AnimatorClothPick.None));
    }

    [Test]
    public void ClickingABoneWhileChoosingCollidersTogglesIt()
    {
        AddDress();
        int before = Dress.Colliders.Count;
        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.PickClothColliders));
        Vector2 head = Placement() * World()[2].Origin;

        AnimatorClothCommands.Click(Session, head, Placement(), World());

        Assert.That(Dress.Colliders.Exists(collider => collider.BoneId == "head"), Is.True);
        Assert.That(Dress.Colliders, Has.Count.EqualTo(before + 1));
    }

    [Test]
    public void InspectorSettingsApplyAndUndo()
    {
        AddDress();

        AnimatorClothCommands.Set(Session, AnimatorClothCommands.Setting.Stiffness, 0.9f);
        AnimatorClothCommands.Set(Session, AnimatorClothCommands.Setting.Rows, 99f);
        AnimatorClothCommands.SetFill(Session, "#ff000080");

        Assert.That(Dress.Stiffness, Is.EqualTo(0.9f));
        Assert.That(Dress.Rows, Is.EqualTo(XafCloth.MaxRows), "rows are clamped");
        Assert.That(Dress.Fill.A, Is.EqualTo(128f / 255f).Within(0.01f));

        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.Undo));
        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.Undo));
        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.Undo));
        Assert.That(Package.Cloths[0].Stiffness, Is.EqualTo(new XafCloth().Stiffness));
    }

    [Test]
    public void TheClothSectionListsTheDressAndTheDeleteButtonRemovesIt()
    {
        AddDress();
        Assert.That(Ui.ClothList.ChildEntities.Count, Is.EqualTo(1));

        Click(Button(ToolActionKind.DeleteCloth));
        Frame(2);

        Assert.That(Package.Cloths, Is.Empty);
        Assert.That(Ui.ClothList.ChildEntities.Count, Is.Zero);
        Assert.That(Session.GetComponent<AnimatorSelection>().Bone, Is.EqualTo(Torso));
    }

    [Test]
    public void ADressSurvivesSavingTheFile()
    {
        AddDress();

        XafPackage saved = XafBinary.Read(XafBinary.Write(Package));

        Assert.That(XafPackageCopy.AreEqual(Package, saved), Is.True);
    }
}

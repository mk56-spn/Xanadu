using System.Collections.Generic;
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
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.Animation.Tool;

/// <summary>
/// Everything about a chain that has to be doable from the editor: choosing its art,
/// hanging it from a bone and moving it there, and placing its collider, by buttons,
/// numbers, clicks on bones and dragging in the preview.
/// </summary>
[TestFixture]
public sealed class AnimatorChainPlacementTests : AnimatorToolTest
{
    protected override void AddSystems()
    {
        Root.Add(new UiTreeSystem());
        Root.Add(new AnimatorActionSystem());
        Root.Add(new AnimatorPreviewInputSystem(Pointer));
        Root.Add(new AnimatorChainInspectorSystem());
        Root.Add(new AnimatorChainSimulationSystem());
        Root.Add(new AnimatorHierarchySystem(new FakeVisualRidFactory()));
    }

    private XafChain Chain => Package.Chains[0];
    private AnimatorChains Chains => Session.GetComponent<AnimatorChains>();
    private string Status => Session.GetComponent<AnimatorStatus>().Message;
    private static string TextOf(Entity entity) => entity.GetComponent<Text>().Value;

    private void SelectBone(int bone)
    {
        Session.GetComponent<AnimatorSelection>().SelectOnlyBone(bone);
        Frame();
    }

    private void AddChainOnHead()
    {
        SelectBone(2);
        Click(Button(ToolActionKind.AddChain));
    }

    private Transform2D[] World() =>
        XafRig.WorldBones(
            Package,
            AnimatorGeometry.PreviewPose(
                Session.GetComponent<AnimatorDocument>(),
                Session.GetComponent<AnimatorModes>()));

    private Vector2 RootScreen() => PreviewOrigin() + XafChainSimulation.Anchor(Package, Chain, World())!.Value;
    private Vector2 ColliderScreen() => PreviewOrigin() + XafChainSimulation.Collider(Package, Chain, World())!.Value;

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

    private void Type(Entity field, string value)
    {
        field.GetComponent<TextField>().Value = value;
        field.AddTag<TextFieldFocused>();
        Frame();
        field.RemoveTag<TextFieldFocused>();
        Frame();
    }

    private Vector2 RootInRestWorld() => RootInRestWorld(Chain);

    private Vector2 RootInRestWorld(XafChain chain) =>
        XafRig.RestWorld(Package)[XafRig.FindBone(Package, chain.BoneId)] * chain.Offset;

    // ---- the inspector shows where everything is

    [Test]
    public void TheButtonsNameTheArtTheBoneAndTheCollider()
    {
        AddChainOnHead();
        Frame();

        Assert.That(TextOf(Ui.ChainArtButton), Is.EqualTo("hair_segment"));
        Assert.That(TextOf(Ui.ChainBoneButton), Is.EqualTo("head"));
        Assert.That(TextOf(Ui.ChainColliderButton), Is.EqualTo("NONE"));

        Chain.CollideBoneId = "torso";
        Chain.CollideRadius = 6f;
        Chain.SvgPath = "";
        Chain.Group = "";
        Frame();

        Assert.That(TextOf(Ui.ChainColliderButton), Is.EqualTo("torso"));
        Assert.That(TextOf(Ui.ChainArtButton), Is.EqualTo("NONE"));
    }

    [Test]
    public void ThePositionFieldsShowAndSetTheOffsetAndTheColliderOffset()
    {
        AddChainOnHead();
        Chain.Offset = new Vector2(-4.5f, -17f);
        Chain.CollideBoneId = "torso";
        Chain.CollideRadius = 9f;
        Chain.CollideOffset = new Vector2(3, 2);
        Frame();

        Assert.That(Ui.ChainOffsetX.GetComponent<TextField>().Value, Is.EqualTo("-4.5"));
        Assert.That(Ui.ChainOffsetY.GetComponent<TextField>().Value, Is.EqualTo("-17"));
        Assert.That(Ui.ChainColliderX.GetComponent<TextField>().Value, Is.EqualTo("3"));
        Assert.That(Ui.ChainColliderY.GetComponent<TextField>().Value, Is.EqualTo("2"));

        Type(Ui.ChainOffsetX, "12");
        Type(Ui.ChainOffsetY, "-30");
        Type(Ui.ChainColliderX, "-6");
        Type(Ui.ChainColliderY, "8");

        Assert.That(Chain.Offset, Is.EqualTo(new Vector2(12, -30)));
        Assert.That(Chain.CollideOffset, Is.EqualTo(new Vector2(-6, 8)));
        // Each typed field is its own undo step.
        AnimatorHistoryCommands.Undo(Session);
        Assert.That(Chain.CollideOffset, Is.EqualTo(new Vector2(-6, 2)));
        Assert.That(Chain.Offset, Is.EqualTo(new Vector2(12, -30)));
    }

    [Test]
    public void TheTransformInspectorGivesUpItsSpotToTheChainInspectorAndGetsItBack()
    {
        Frame();
        Assert.That(Ui.Inspector.GetComponent<Position>().Value, Is.EqualTo(AnimatorLayout.InspectorCenter(Screen)));

        AddChainOnHead();
        Assert.That(Ui.Inspector.GetComponent<Position>().Value, Is.EqualTo(UiLayout.Hidden));
        Assert.That(
            Ui.ChainInspector.GetComponent<Position>().Value,
            Is.EqualTo(AnimatorLayout.ChainInspectorTopLeft(Screen) + AnimatorLayout.ChainInspectorSize / 2));

        SelectBone(1);
        Assert.That(Ui.Inspector.GetComponent<Position>().Value, Is.EqualTo(AnimatorLayout.InspectorCenter(Screen)));
        Assert.That(Ui.ChainInspector.GetComponent<Position>().Value, Is.EqualTo(UiLayout.Hidden));
    }

    [Test]
    public void EveryChainFieldFitsInsideTheInspectorAndTheInspectorEndsAboveTheTimeline()
    {
        AddChainOnHead();
        Rect2 inspector = AnimatorGeometry.PanelArea(Ui.ChainInspector);
        Rect2 timeline = AnimatorGeometry.PanelArea(Ui.Timeline);

        Assert.That(inspector.End.Y, Is.LessThanOrEqualTo(timeline.Position.Y));
        Assert.That(inspector.End.X, Is.LessThanOrEqualTo(Screen.X));
        foreach (Entity control in new[]
                 {
                     Ui.ChainArtButton, Ui.ChainBoneButton, Ui.ChainOffsetX, Ui.ChainOffsetY, Ui.ChainSegments,
                     Ui.ChainLength, Ui.ChainWidth, Ui.ChainGravity, Ui.ChainLean, Ui.ChainDamping,
                     Ui.ChainColliderButton, Ui.ChainColliderX, Ui.ChainColliderY, Ui.ChainCollide,
                 })
        {
            Assert.That(inspector.Encloses(AnimatorGeometry.PanelArea(control).Grow(-0.5f)), Is.True);
        }
    }

    // ---- art

    [Test]
    public void TheArtButtonOpensThePickerForTheChainAndChoosingARowSetsItsArtAsOneUndoStep()
    {
        AddChainOnHead();

        Click(Button(ToolActionKind.PickChainArt));

        AnimatorSvgMenu menu = Session.GetComponent<AnimatorSvgMenu>();
        Assert.That(menu.Open, Is.True);
        Assert.That(menu.ForChain, Is.True);
        Assert.That(menu.Anchor, Is.EqualTo(AnimatorLayout.ChainPickerAnchor(Screen)));

        Session.GetComponent<AnimatorSvgMenu>().Choices = new List<AnimatorSvgChoice>
        {
            new("res://Assets/SVG/leaf.svg", "leaf_piece"),
        };
        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.AttachSvg, "0"));

        Assert.That(Chain.SvgPath, Is.EqualTo("res://Assets/SVG/leaf.svg"));
        Assert.That(Chain.Group, Is.EqualTo("leaf_piece"));
        Assert.That(Package.Attachments, Has.Count.EqualTo(1), "a chain's art is not a new attachment");
        Assert.That(Session.GetComponent<AnimatorSvgMenu>().Open, Is.False, "the picker closes once the art is chosen");
        Assert.That(Status, Does.StartWith("ART"));

        Frame();
        Assert.That(TextOf(Ui.ChainArtButton), Is.EqualTo("leaf / leaf_piece"));

        AnimatorHistoryCommands.Undo(Session);
        Assert.That(Package.Chains[0].SvgPath, Is.EqualTo(XafChain.DefaultSvgPath));
    }

    [Test]
    public void ChoosingArtRebuildsTheVisualsSoItIsLoaded()
    {
        AddChainOnHead();
        int version = Session.GetComponent<AnimatorDocument>().Version;

        Click(Button(ToolActionKind.PickChainArt));
        Session.GetComponent<AnimatorSvgMenu>().Choices = new List<AnimatorSvgChoice> { new("res://a.svg", "a") };
        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.AttachSvg, "0"));

        Assert.That(Session.GetComponent<AnimatorDocument>().Version, Is.GreaterThan(version));
    }

    [Test]
    public void ThePickerOpenedFromTheBoneMenuStillAddsAnAttachmentNotChainArt()
    {
        AddChainOnHead();
        Click(Button(ToolActionKind.PickChainArt));
        Assert.That(Session.GetComponent<AnimatorSvgMenu>().ForChain, Is.True);
        Session.GetComponent<AnimatorSvgMenu>().Open = false;

        SelectBone(1);
        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.AddSvg));
        Assert.That(Session.GetComponent<AnimatorSvgMenu>().ForChain, Is.False);

        Session.GetComponent<AnimatorSvgMenu>().Choices = new List<AnimatorSvgChoice> { new("res://a.svg", "a") };
        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.AttachSvg, "0"));

        Assert.That(Package.Attachments, Has.Count.EqualTo(2));
        Assert.That(Chain.SvgPath, Is.EqualTo(XafChain.DefaultSvgPath));
    }

    [Test]
    public void TheArtPickerNeedsASelectedChain()
    {
        SelectBone(2);

        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.PickChainArt));

        Assert.That(Session.GetComponent<AnimatorSvgMenu>().Open, Is.False);
        Assert.That(Status, Is.EqualTo("SELECT A CHAIN FIRST"));
    }

    // ---- hanging from another bone

    [Test]
    public void ClickingTheBoneButtonThenABoneInThePreviewRehangsTheChainWithoutMovingIt()
    {
        AddChainOnHead();
        Chain.Offset = new Vector2(-4, -17);
        Vector2 before = RootInRestWorld();

        Click(Button(ToolActionKind.PickChainBone));
        Assert.That(Chains.Picking, Is.EqualTo(AnimatorChainPick.Anchor));
        Assert.That(Status, Does.Contain("CLICK THE BONE head_chain HANGS FROM"));
        Assert.That(TextOf(Ui.ChainBoneButton), Is.EqualTo("CLICK A BONE"));

        PressAt(BoneScreenPosition(1));
        Release();

        Assert.That(Chain.BoneId, Is.EqualTo("torso"));
        Assert.That(RootInRestWorld().DistanceTo(before), Is.LessThan(0.001f));
        Assert.That(Chains.Picking, Is.EqualTo(AnimatorChainPick.None));
        Assert.That(Session.GetComponent<AnimatorSelection>().Chain, Is.EqualTo(0), "the chain stays selected");
        Assert.That(Status, Is.EqualTo("head_chain HANGS FROM torso"));
        Frame();
        Assert.That(TextOf(Ui.ChainBoneButton), Is.EqualTo("torso"));

        AnimatorHistoryCommands.Undo(Session);
        Assert.That(Package.Chains[0].BoneId, Is.EqualTo("head"));
        Assert.That(Package.Chains[0].Offset, Is.EqualTo(new Vector2(-4, -17)));
    }

    [Test]
    public void ABoneClickedInTheTreeAlsoGivesThePick()
    {
        AddChainOnHead();
        Click(Button(ToolActionKind.PickChainBone));
        Ui.BoneList.AddTag<LayoutDirty>();
        Frame(2);

        Click(BoneLabel("root"));

        Assert.That(Chain.BoneId, Is.EqualTo("root"));
        Assert.That(Session.GetComponent<AnimatorSelection>().Chain, Is.EqualTo(0), "picking did not select the bone");
        Assert.That(Chains.Picking, Is.EqualTo(AnimatorChainPick.None));
    }

    [Test]
    public void ClickingNoBoneCancelsThePickAndChangesNothing()
    {
        AddChainOnHead();
        Click(Button(ToolActionKind.PickChainBone));

        PressAt(PreviewOrigin() + new Vector2(250, -120));
        Release();

        Assert.That(Chain.BoneId, Is.EqualTo("head"));
        Assert.That(Chains.Picking, Is.EqualTo(AnimatorChainPick.None));
        Assert.That(Status, Is.EqualTo("CANCELLED"));
    }

    [Test]
    public void PressingAnyOtherButtonDropsAPendingPick()
    {
        AddChainOnHead();
        Click(Button(ToolActionKind.PickChainBone));
        Assert.That(Chains.Picking, Is.EqualTo(AnimatorChainPick.Anchor));

        Click(Button(ToolActionKind.FitPreview));
        Assert.That(Chains.Picking, Is.EqualTo(AnimatorChainPick.None));

        Click(Button(ToolActionKind.PickColliderBone));
        Click(Button(ToolActionKind.PickChainBone));
        Assert.That(Chains.Picking, Is.EqualTo(AnimatorChainPick.Anchor), "the latest pick button wins");
    }

    // ---- collider

    [Test]
    public void TheColliderBoneButtonThenABoneGivesTheChainAColliderThere()
    {
        AddChainOnHead();
        Assert.That(Chain.HasCollider, Is.False);

        Click(Button(ToolActionKind.PickColliderBone));
        Assert.That(Chains.Picking, Is.EqualTo(AnimatorChainPick.Collider));
        Assert.That(TextOf(Ui.ChainColliderButton), Is.EqualTo("CLICK A BONE"));
        PressAt(BoneScreenPosition(1));
        Release();

        Assert.That(Chain.CollideBoneId, Is.EqualTo("torso"));
        Assert.That(Chain.CollideRadius, Is.EqualTo(XafChainAuthoring.DefaultColliderRadius));
        Assert.That(Chain.HasCollider, Is.True);
        Assert.That(Status, Is.EqualTo("head_chain COLLIDES WITH torso"));
        Frame();
        Assert.That(TextOf(Ui.ChainColliderButton), Is.EqualTo("torso"));
        Assert.That(Ui.ChainCollide.GetComponent<TextField>().Value, Is.EqualTo("8"));
        Assert.That(Chain.BoneId, Is.EqualTo("head"), "the chain itself did not move");
    }

    [Test]
    public void TheColliderSizeCanBeTypedAndZeroRemovesTheCollider()
    {
        AddChainOnHead();
        XafChainAuthoring.SetColliderBone(Package, 0, 1);
        Frame();

        Type(Ui.ChainCollide, "14");
        Assert.That(Chain.CollideRadius, Is.EqualTo(14f));

        Type(Ui.ChainCollide, "0");
        Assert.That(Chain.HasCollider, Is.False);
        Frame();
        Assert.That(TextOf(Ui.ChainColliderButton), Is.EqualTo("NONE"));
    }

    // ---- dragging in the preview

    [Test]
    public void DraggingTheRootDotMovesTheChainAlongItsBoneAsOneUndoStep()
    {
        AddChainOnHead();
        Vector2 start = RootScreen();

        PressAt(start);
        Assert.That(Session.GetComponent<AnimatorDrag>().Kind, Is.EqualTo(AnimatorDragKind.ChainRoot));
        MoveTo(start + new Vector2(20, 10));
        MoveTo(start + new Vector2(30, 15));
        Release();

        Assert.That(Chain.Offset, Is.EqualTo(new Vector2(30, 15)).Using<Vector2>((a, b) => a.DistanceTo(b) < 0.01f ? 0 : 1));
        Assert.That(Session.GetComponent<AnimatorSelection>().Chain, Is.EqualTo(0));
        Assert.That(Session.GetComponent<AnimatorDrag>().Kind, Is.EqualTo(AnimatorDragKind.None));
        Assert.That(Ui.ChainOffsetX.GetComponent<TextField>().Value, Is.EqualTo("30"));

        AnimatorHistoryCommands.Undo(Session);
        Assert.That(Package.Chains[0].Offset, Is.EqualTo(Vector2.Zero));
    }

    [Test]
    public void TheRootDotWinsOverTheBoneJointUnderIt()
    {
        AddChainOnHead();
        // The chain hangs from the head's own origin, exactly where the head's joint is drawn.
        Assert.That(RootScreen().DistanceTo(BoneScreenPosition(2)), Is.LessThan(0.01f));

        PressAt(BoneScreenPosition(2));

        Assert.That(Session.GetComponent<AnimatorDrag>().Kind, Is.EqualTo(AnimatorDragKind.ChainRoot));
        Assert.That(Session.GetComponent<AnimatorSelection>().Chain, Is.EqualTo(0));
        Assert.That(Session.GetComponent<AnimatorSelection>().Bone, Is.EqualTo(-1));
    }

    [Test]
    public void DraggingTheColliderRingsCentreMovesTheColliderAlongItsBone()
    {
        AddChainOnHead();
        XafChainAuthoring.SetColliderBone(Package, 0, 1);
        XafChainAuthoring.SetCollideOffset(Package, 0, new Vector2(30, 0));
        Frame();
        Vector2 start = ColliderScreen();

        PressAt(start);
        Assert.That(Session.GetComponent<AnimatorDrag>().Kind, Is.EqualTo(AnimatorDragKind.ChainCollider));
        MoveTo(start + new Vector2(10, 5));
        Release();

        Assert.That(Chain.CollideOffset.DistanceTo(new Vector2(40, 5)), Is.LessThan(0.01f));
        Assert.That(Chain.Offset, Is.EqualTo(Vector2.Zero), "the root did not move");

        AnimatorHistoryCommands.Undo(Session);
        Assert.That(Package.Chains[0].CollideOffset, Is.EqualTo(new Vector2(30, 0)));
    }

    [Test]
    public void ADragFollowsTheBonesRotationSoTheOffsetStaysInTheBonesOwnSpace()
    {
        AddChainOnHead();
        // Turn the head a quarter turn in the rest pose, so its local X points down the screen.
        Package.Skeleton.Bones[2].Rest = new Transform2D(Mathf.Pi / 2f, Package.Skeleton.Bones[2].Rest.Origin);
        Session.GetComponent<AnimatorModes>().Rest = true;
        Frame();
        Vector2 start = RootScreen();

        PressAt(start);
        MoveTo(start + new Vector2(0, 20));
        Release();

        Assert.That(Chain.Offset.DistanceTo(new Vector2(20, 0)), Is.LessThan(0.01f));
    }

    [Test]
    public void ClickingAwayFromTheHandlesStillSelectsBonesAsBefore()
    {
        AddChainOnHead();

        PressAt(BoneScreenPosition(1));
        Release();

        Assert.That(Session.GetComponent<AnimatorDrag>().Kind, Is.Not.EqualTo(AnimatorDragKind.ChainRoot));
        Assert.That(Session.GetComponent<AnimatorSelection>().Chain, Is.EqualTo(-1));
        Assert.That(Chain.Offset, Is.EqualTo(Vector2.Zero));
    }

    [Test]
    public void NothingIsGrabbableWhenNoChainIsSelected()
    {
        AddChainOnHead();
        SelectBone(1);

        PressAt(BoneScreenPosition(2));

        Assert.That(AnimatorChainHandles.IsChainDrag(Session.GetComponent<AnimatorDrag>().Kind), Is.False);
    }
}

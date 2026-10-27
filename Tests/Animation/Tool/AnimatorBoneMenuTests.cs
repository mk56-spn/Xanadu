using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Systems.Ui;
using Xanadu.Tests.Support;
using Xanadu.Tools.Animator;
using Xanadu.Tools.Animator.Systems;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.Animation.Tool;

[TestFixture]
public sealed class AnimatorBoneMenuTests : AnimatorToolTest
{
    protected override void AddSystems()
    {
        Root.Add(new UiTreeSystem());
        Root.Add(new AnimatorActionSystem());
        Root.Add(new AnimatorBoneMenuSystem(Pointer));
        Root.Add(new AnimatorPreviewInputSystem(Pointer));
        Root.Add(new AnimatorHierarchySystem(new FakeVisualRidFactory()));
    }

    private Vector2 TreePoint(string boneId)
    {
        // The headless harness leaves the rows unstacked until the tree is laid out again.
        Ui.BoneList.AddTag<LayoutDirty>();
        Frame(2);
        return AnimatorGeometry.PanelArea(BoneLabel(boneId)).GetCenter();
    }

    private AnimatorBoneMenu Menu => Session.GetComponent<AnimatorBoneMenu>();

    private Vector2 LabelPoint(int bone) =>
        AnimatorGeometry.BoneLabel(BoneScreenPosition(bone)).GetCenter() + new Vector2(20, 0);

    private void RightClick(Vector2 position)
    {
        Pointer.Position = position;
        Pointer.Secondary = true;
        Frame();
        Pointer.Secondary = false;
        Frame();
    }

    /// <summary>Clicks a menu option as the UI input system would: pointer down over it, then up.</summary>
    private void ChooseOption(ToolActionKind kind)
    {
        Entity option = MenuOption(kind);
        Pointer.Pressed = true;
        option.AddTag<Pressed>();
        Frame();
        Pointer.Pressed = false;
        option.RemoveTag<Pressed>();
        Frame();
    }

    private Entity MenuOption(ToolActionKind kind)
    {
        foreach (Entity child in Ui.BoneMenu.ChildEntities)
        {
            if (child.GetComponent<ToolAction>().Kind == kind)
                return child;
        }

        Assert.Fail($"No menu option {kind}.");
        return default;
    }

    private void ClickPreview(Vector2 position)
    {
        Pointer.Position = position;
        Pointer.Pressed = true;
        Ui.Preview.AddTag<Pressed>();
        Frame();
        Pointer.Pressed = false;
        Ui.Preview.RemoveTag<Pressed>();
        Frame();
    }

    [Test]
    public void RightClickingABoneLabelSelectsItAndOpensTheMenuThere()
    {
        Vector2 point = LabelPoint(2);

        RightClick(point);

        Assert.That(Session.GetComponent<AnimatorSelection>().Bone, Is.EqualTo(2));
        Assert.That(Menu.Open, Is.True);
        Vector2 center = Ui.BoneMenu.GetComponent<Position>().Value;
        Assert.That(center, Is.EqualTo(point + AnimatorLayout.BoneMenuSize / 2));
    }

    [Test]
    public void RightClickingEmptySpaceOrAnotherClickClosesTheMenu()
    {
        RightClick(LabelPoint(1));
        RightClick(PreviewOrigin() + new Vector2(300, 200));
        Assert.That(Menu.Open, Is.False);

        RightClick(LabelPoint(1));
        ClickPreview(PreviewOrigin() + new Vector2(300, 200));
        Assert.That(Menu.Open, Is.False);
        Assert.That(Ui.BoneMenu.GetComponent<Position>().Value, Is.EqualTo(UiLayout.Hidden));
    }

    [Test]
    public void AddChildAddsABoneUnderTheClickedOne()
    {
        RightClick(LabelPoint(2));

        ChooseOption(ToolActionKind.AddBone);

        Assert.That(Package.Skeleton.Bones, Has.Count.EqualTo(4));
        Assert.That(Package.Skeleton.Bones[3].Parent, Is.EqualTo(2));
        Assert.That(Menu.Open, Is.False);
    }

    [Test]
    public void DeleteRemovesTheClickedBone()
    {
        RightClick(LabelPoint(2));

        ChooseOption(ToolActionKind.DeleteBone);

        Assert.That(Package.Skeleton.Bones.Exists(bone => bone.Id == "head"), Is.False);
    }

    [Test]
    public void RenameFocusesAFieldOverTheLabelAndRenamesOnSubmit()
    {
        RightClick(LabelPoint(2));
        ChooseOption(ToolActionKind.RenameBone);

        Entity field = Ui.BoneNameField;
        Assert.That(field.Tags.Has<TextFieldFocused>(), Is.True);
        Assert.That(field.GetComponent<TextField>().Value, Is.EqualTo("head"));
        Assert.That(field.GetComponent<Position>().Value, Is.Not.EqualTo(UiLayout.Hidden));

        field.GetComponent<TextField>().Value = " skull ";
        field.RemoveTag<TextFieldFocused>();
        Frame();

        Assert.That(Package.Skeleton.Bones[2].Id, Is.EqualTo("skull"));
        Assert.That(Idle.Channels[0].TargetId, Is.EqualTo("skull"));
        Assert.That(field.GetComponent<Position>().Value, Is.EqualTo(UiLayout.Hidden));
    }

    [Test]
    public void RenamingToATakenNameKeepsTheOldName()
    {
        RightClick(LabelPoint(2));
        ChooseOption(ToolActionKind.RenameBone);

        Ui.BoneNameField.GetComponent<TextField>().Value = "torso";
        Ui.BoneNameField.RemoveTag<TextFieldFocused>();
        Frame();

        Assert.That(Package.Skeleton.Bones[2].Id, Is.EqualTo("head"));
    }

    [Test]
    public void ReparentMovesTheBoneUnderTheNextClickedBone()
    {
        RightClick(LabelPoint(2));
        ChooseOption(ToolActionKind.ReparentBone);
        Assert.That(Menu.Reparenting, Is.EqualTo(2));
        Vector2 headBefore = BoneScreenPosition(2);

        ClickPreview(BoneScreenPosition(0));

        Assert.That(Package.Skeleton.Bones[2].Parent, Is.EqualTo(0));
        Assert.That(Menu.Reparenting, Is.EqualTo(-1));
        Assert.That(Session.GetComponent<AnimatorSelection>().Bone, Is.EqualTo(2));
        Assert.That(BoneScreenPosition(2).DistanceTo(headBefore), Is.LessThan(0.01f));
    }

    [Test]
    public void ReparentingIntoItsOwnChildIsRefused()
    {
        RightClick(LabelPoint(1));
        ChooseOption(ToolActionKind.ReparentBone);

        ClickPreview(LabelPoint(2));

        Assert.That(Package.Skeleton.Bones[2].Parent, Is.EqualTo(1));
        Assert.That(Package.Skeleton.Bones[1].Parent, Is.EqualTo(0));
    }

    [Test]
    public void ClickingEmptySpaceCancelsAReparent()
    {
        RightClick(LabelPoint(2));
        ChooseOption(ToolActionKind.ReparentBone);

        ClickPreview(PreviewOrigin() + new Vector2(300, 200));

        Assert.That(Menu.Reparenting, Is.EqualTo(-1));
        Assert.That(Package.Skeleton.Bones[2].Parent, Is.EqualTo(1));
    }

    [Test]
    public void RightClickingABoneInTheTreeOpensTheMenuForIt()
    {
        Vector2 point = TreePoint("head");

        RightClick(point);

        Assert.That(Session.GetComponent<AnimatorSelection>().Bone, Is.EqualTo(2));
        Assert.That(Menu.Open, Is.True);
        Assert.That(Menu.FromTree, Is.True);
        Assert.That(Ui.BoneMenu.GetComponent<Position>().Value,
            Is.EqualTo(point + AnimatorLayout.BoneMenuSize / 2));
    }

    [Test]
    public void RenamingFromTheTreeEditsOverTheTreeRow()
    {
        RightClick(TreePoint("head"));
        ChooseOption(ToolActionKind.RenameBone);

        Rect2 row = AnimatorGeometry.PanelArea(BoneLabel("head"));
        Vector2 field = Ui.BoneNameField.GetComponent<Position>().Value;
        Assert.That(row.HasPoint(field - new Vector2(AnimatorLayout.BoneNameSize.X / 2 - 2, 0)), Is.True);

        Ui.BoneNameField.GetComponent<TextField>().Value = "skull";
        Ui.BoneNameField.RemoveTag<TextFieldFocused>();
        Frame(2);

        Assert.That(Package.Skeleton.Bones[2].Id, Is.EqualTo("skull"));
        Assert.That(UiTreeControl.FindNode(Ui.BoneList, "skull").IsNull, Is.False);
    }

    [Test]
    public void ReparentTakesTheNewParentFromATreeClick()
    {
        RightClick(TreePoint("head"));
        ChooseOption(ToolActionKind.ReparentBone);

        Click(BoneLabel("root"));

        Assert.That(Package.Skeleton.Bones[2].Parent, Is.EqualTo(0));
        Assert.That(Menu.Reparenting, Is.EqualTo(-1));
        Assert.That(Session.GetComponent<AnimatorSelection>().Bone, Is.EqualTo(2));
    }

    [Test]
    public void DeleteFromTheTreeRemovesThatBone()
    {
        RightClick(TreePoint("head"));

        ChooseOption(ToolActionKind.DeleteBone);

        Assert.That(Package.Skeleton.Bones.Exists(bone => bone.Id == "head"), Is.False);
    }
}

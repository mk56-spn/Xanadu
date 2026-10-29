using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Systems.Ui;
using Xanadu.Ecs;
using Xanadu.Tests.Support;
using Xanadu.Tools.Animator;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Systems;

namespace Xanadu.Tests.Animation.Tool;

[TestFixture]
public sealed class AnimatorHierarchyTests : AnimatorToolTest
{
    protected override void AddSystems()
    {
        Root.Add(new UiTreeSystem());
        Root.Add(new AnimatorActionSystem());
        Root.Add(new AnimatorHierarchySystem(new FakeVisualRidFactory()));
    }

    private static List<string> Labels(Entity list)
    {
        var labels = new List<string>();
        foreach (Entity child in list.ChildEntities)
            labels.Add(child.GetComponent<Text>().Value);
        return labels;
    }

    private List<string> ClipLabels()
    {
        var options = new List<(int Value, string Label)>();
        foreach (Entity option in Store.Query<ToolAction, DropdownOption>().ToEntityList())
        {
            if (option.GetComponent<ToolAction>().Kind == ToolActionKind.Clip)
            {
                options.Add((option.GetComponent<DropdownOption>().Value,
                    option.GetComponent<Text>().Value));
            }
        }

        options.Sort((a, b) => a.Value.CompareTo(b.Value));
        return options.ConvertAll(option => option.Label);
    }

    [Test]
    public void ClipsAreChosenFromADropdownWhoseButtonShowsTheCurrentClip()
    {
        Frame();

        Entity button = Ui.ClipDropdown;
        Assert.That(button.HasComponent<Dropdown>(), Is.True);
        Assert.That(button.GetComponent<Text>().Value, Is.EqualTo("IDLE"));

        Click(Button(ToolActionKind.Clip, "jump"));
        Assert.That(button.GetComponent<Text>().Value, Is.EqualTo("JUMP"));
    }

    [Test]
    public void TheDropdownIsRebuiltWhenTheClipListChanges()
    {
        Entity before = Ui.ClipDropdown;
        AnimatorClipCommands.Create(Session, "clip", 1f, false);
        Frame(2);

        Assert.That(Ui.ClipDropdown.Id, Is.Not.EqualTo(before.Id));
        Assert.That(ClipLabels(), Is.EqualTo(new[] { "IDLE", "JUMP", "CLIP" }));
        Assert.That(Ui.ClipDropdown.GetComponent<Text>().Value, Is.EqualTo("CLIP"));
        // The clip dropdown, the FILE menu and the CONFIG menu.
        Assert.That(Store.Query<Dropdown>().Count, Is.EqualTo(3));
    }

    [Test]
    public void RebuildingTheDropdownLeavesNoOrphanedOptions()
    {
        for (int i = 0; i < 3; i++)
        {
            AnimatorClipCommands.Create(Session, $"clip{i}", 1f, false);
            Frame(2);
        }

        Assert.That(ClipLabels(), Has.Count.EqualTo(Package.Clips.Count));
        // The FILE menu's four entries, the CONFIG menu's two, and one option per clip.
        Assert.That(Store.Query<DropdownOption>().Count, Is.EqualTo(4 + 2 + Package.Clips.Count));
    }

    [Test]
    public void ListsEveryBoneAttachmentAndClip()
    {
        Assert.That(VisibleBones(), Is.EqualTo(new[] { "root", "torso", "head" }));
        Assert.That(Labels(Ui.AttachmentList), Is.EqualTo(new[] { "body" }));
        Assert.That(ClipLabels(), Is.EqualTo(new[] { "IDLE", "JUMP" }));
    }

    [Test]
    public void AddingABoneAddsAnEntryWithoutDuplicatingTheRest()
    {
        Click(Button(ToolActionKind.AddBone));

        Assert.That(VisibleBones(),
            Is.EqualTo(new[] { "root", "torso", "head", "bone-1" }));
    }

    [Test]
    public void LoadingAPackageRefreshesTheClipList()
    {
        var other = XafRigAuthoring.CreateEmptyPackage();
        other.Clips.Clear();
        other.Clips.Add(new XafClip { Id = "walk", Duration = 1f });
        Files.ToLoad = other;

        Click(Button(ToolActionKind.Load));

        Assert.That(ClipLabels(), Is.EqualTo(new[] { "WALK" }));
        Assert.That(VisibleBones(), Is.EqualTo(new[] { "root" }));
        Assert.That(Labels(Ui.AttachmentList), Is.Empty);
    }

    [Test]
    public void BonesAreShownAsATreeMatchingTheRigHierarchy()
    {
        UiTreeNode torso = UiTreeControl.FindNode(Ui.BoneList, "torso").GetComponent<UiTreeNode>();
        UiTreeNode head = UiTreeControl.FindNode(Ui.BoneList, "head").GetComponent<UiTreeNode>();

        Assert.That(UiTreeControl.FindNode(Ui.BoneList, "root").GetComponent<UiTreeNode>().Depth, Is.EqualTo(0));
        Assert.That(torso.Depth, Is.EqualTo(1));
        Assert.That(torso.Parent, Is.EqualTo("root"));
        Assert.That(head.Depth, Is.EqualTo(2));
        Assert.That(head.Parent, Is.EqualTo("torso"));
    }

    [Test]
    public void ClickingABoneInTheTreeSelectsItAndHighlightsIt()
    {
        Click(BoneLabel("torso"));

        Assert.That(Session.GetComponent<AnimatorSelection>().Bone, Is.EqualTo(1));
        Assert.That(BoneLabel("torso").GetComponent<ButtonVisual>().Normal, Is.EqualTo(UiTheme.Selected));
        Assert.That(BoneLabel("head").GetComponent<ButtonVisual>().Normal, Is.Not.EqualTo(UiTheme.Selected));
        Assert.That(Ui.BoneList.HasComponent<UiTreeChanged>(), Is.False);
    }

    [Test]
    public void ASelectionMadeElsewhereShowsInTheTree()
    {
        Session.GetComponent<AnimatorSelection>().Bone = 2;
        Frame(2);

        Assert.That(UiTreeControl.Selected(Ui.BoneList), Is.EqualTo("head"));

        Session.GetComponent<AnimatorSelection>().Bone = -1;
        Frame(2);
        Assert.That(UiTreeControl.Selected(Ui.BoneList), Is.EqualTo(""));
    }

    [Test]
    public void SelectingAHiddenBoneElsewhereExpandsToShowIt()
    {
        Click(BoneArrow("torso"));
        Assert.That(VisibleBones(), Is.EqualTo(new[] { "root", "torso" }));

        Session.GetComponent<AnimatorSelection>().Bone = 2;
        Frame(3);

        Assert.That(VisibleBones(), Is.EqualTo(new[] { "root", "torso", "head" }));
    }

    [Test]
    public void CollapsingTheBranchOfTheSelectedBoneStaysCollapsed()
    {
        Session.GetComponent<AnimatorSelection>().Bone = 2;
        Frame(2);

        Click(BoneArrow("torso"));
        Frame(5);

        Assert.That(VisibleBones(), Is.EqualTo(new[] { "root", "torso" }));
    }

    [Test]
    public void TheTreeKeepsItsCollapsedBranchesWhenTheRigChanges()
    {
        Click(BoneArrow("torso"));

        Click(Button(ToolActionKind.AddBone));

        Assert.That(VisibleBones(), Does.Not.Contain("head"));
        Assert.That(VisibleBones(), Does.Contain("bone-1"));
    }

    [Test]
    public void AttachmentSelectionStillHighlightsItsEntry()
    {
        Click(Button(ToolActionKind.SelectAttachment, "0"));

        Color selected = Button(ToolActionKind.SelectAttachment, "0")
            .GetComponent<ButtonVisual>().Normal;
        Assert.That(selected, Is.Not.EqualTo(UiTheme.Button));
    }
}

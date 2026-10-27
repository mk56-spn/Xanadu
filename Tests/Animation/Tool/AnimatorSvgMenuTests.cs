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

[TestFixture]
public sealed class AnimatorSvgMenuTests : AnimatorToolTest
{
    private readonly FakeAnimatorSvgCatalog _catalog = new();

    protected override void AddSystems()
    {
        Root.Add(new UiTreeSystem());
        Root.Add(new AnimatorActionSystem());
        Root.Add(new AnimatorBoneMenuSystem(Pointer));
        Root.Add(new AnimatorSvgMenuSystem(_catalog, new FakeVisualRidFactory(), Pointer));
        Root.Add(new AnimatorHierarchySystem(new FakeVisualRidFactory()));
    }

    private AnimatorSvgMenu Menu => Session.GetComponent<AnimatorSvgMenu>();

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

    private Entity BoneOption(ToolActionKind kind)
    {
        foreach (Entity child in Ui.BoneMenu.ChildEntities)
        {
            if (child.GetComponent<ToolAction>().Kind == kind)
                return child;
        }

        Assert.Fail($"No bone menu option {kind}.");
        return default;
    }

    /// <summary>Right-clicks a bone, then clicks ADD SVG in its menu.</summary>
    private void OpenPicker(int bone)
    {
        RightClick(LabelPoint(bone));
        Click(BoneOption(ToolActionKind.AddSvg));
        Frame(2);
    }

    private List<Entity> Rows()
    {
        var rows = new List<Entity>();
        foreach (Entity child in Ui.SvgMenuList.ChildEntities)
            rows.Add(child);
        return rows;
    }

    private void ClickRow(Entity row)
    {
        // UI positions are relative to the parent, so add up the chain to get
        // the screen point a click on the row would land on.
        Pointer.Position = Ui.SvgMenu.GetComponent<Position>().Value +
                           Ui.SvgMenuList.GetComponent<Position>().Value +
                           row.GetComponent<Position>().Value;
        Pointer.Pressed = true;
        row.AddTag<Pressed>();
        Frame();
        Pointer.Pressed = false;
        row.RemoveTag<Pressed>();
        Frame();
    }

    [SetUp]
    public void UseOneFileWithTwoGroups()
    {
        _catalog.GroupsByFile.Clear();
        _catalog.Add("res://Assets/SVG/body.svg", "body");
        _catalog.Add("res://Assets/SVG/arm.svg", "arm", "sleeve");
    }

    [Test]
    public void AddSvgOpensThePickerOnTheBoneAndOffersEveryGroup()
    {
        OpenPicker(2);

        Assert.That(Menu.Open, Is.True);
        Assert.That(Menu.Bone, Is.EqualTo(2));
        Assert.That(
            Menu.Choices,
            Is.EqualTo(new[]
            {
                new AnimatorSvgChoice("res://Assets/SVG/arm.svg", "arm"),
                new AnimatorSvgChoice("res://Assets/SVG/arm.svg", "sleeve"),
                new AnimatorSvgChoice("res://Assets/SVG/body.svg", "body"),
            }));
        Assert.That(Ui.SvgMenu.GetComponent<Position>().Value, Is.Not.EqualTo(UiLayout.Hidden));
    }

    [Test]
    public void TheRowsNameEachChoiceAndAttachIt()
    {
        OpenPicker(2);

        List<Entity> rows = Rows();
        Assert.That(rows.Count, Is.EqualTo(3));
        foreach (Entity row in rows)
            Assert.That(row.GetComponent<ToolAction>().Kind, Is.EqualTo(ToolActionKind.AttachSvg));

        // The file's name alone when it matches the group, both when it does not.
        Assert.That(rows[0].GetComponent<Text>().Value, Is.EqualTo("arm"));
        Assert.That(rows[1].GetComponent<Text>().Value, Is.EqualTo("arm / sleeve"));
    }

    [Test]
    public void ChoosingARowAddsTheGroupToTheBoneAndKeepsThePickerOpen()
    {
        OpenPicker(2);
        int before = Package.Attachments.Count;

        ClickRow(Rows()[1]);

        Assert.That(Package.Attachments.Count, Is.EqualTo(before + 1));
        XafAttachment added = Package.Attachments[^1];
        Assert.That(added.SvgPath, Is.EqualTo("res://Assets/SVG/arm.svg"));
        Assert.That(added.Group, Is.EqualTo("sleeve"));
        Assert.That(added.BoneId, Is.EqualTo("head"));
        Assert.That(Session.GetComponent<AnimatorSelection>().Attachment, Is.EqualTo(before));
        Assert.That(Session.GetComponent<AnimatorSelection>().Bone, Is.EqualTo(-1));
        Assert.That(Session.GetComponent<AnimatorStatus>().Message,
            Is.EqualTo("ADDED sleeve TO head"));
        Assert.That(Session.GetComponent<AnimatorFile>().Dirty, Is.True);
        Assert.That(Menu.Open, Is.True);
    }

    [Test]
    public void TwoGroupsCanBeAddedWithoutReopeningThePicker()
    {
        OpenPicker(2);
        int before = Package.Attachments.Count;

        ClickRow(Rows()[0]);
        ClickRow(Rows()[1]);

        Assert.That(Package.Attachments.Count, Is.EqualTo(before + 2));
    }

    [Test]
    public void UndoRemovesTheAddedAttachment()
    {
        OpenPicker(2);
        ClickRow(Rows()[0]);

        AnimatorHistoryCommands.Undo(Session);

        Assert.That(Package.Attachments, Has.Count.EqualTo(1));
        Assert.That(Session.GetComponent<AnimatorSelection>().Attachment, Is.EqualTo(-1));
    }

    [Test]
    public void ClickingOffThePickerClosesItAndRemovesItsRows()
    {
        OpenPicker(2);

        Pointer.Position = new Vector2(5, 5);
        Pointer.Pressed = true;
        Frame();
        Pointer.Pressed = false;
        Frame(2);

        Assert.That(Menu.Open, Is.False);
        Assert.That(Rows(), Is.Empty);
        Assert.That(Ui.SvgMenu.GetComponent<Position>().Value, Is.EqualTo(UiLayout.Hidden));
    }

    [Test]
    public void ManyGroupsShowAScrollingWindowOfRows()
    {
        _catalog.Add("res://Assets/SVG/sheet.svg", ManyGroups(25));

        OpenPicker(2);

        Assert.That(Rows().Count, Is.EqualTo(2 + 1 + 25));
        Assert.That(
            Ui.SvgMenu.GetComponent<Rectangle>().Bounds.Size.Y,
            Is.EqualTo(AnimatorLayout.SvgMenuHeight(AnimatorLayout.SvgMenuRows)));
    }

    [Test]
    public void AProjectWithoutSvgArtSaysSoInsteadOfOpening()
    {
        _catalog.GroupsByFile.Clear();
        RightClick(LabelPoint(2));

        Click(BoneOption(ToolActionKind.AddSvg));

        Assert.That(Menu.Open, Is.False);
        Assert.That(Session.GetComponent<AnimatorStatus>().Message,
            Is.EqualTo("NO SVG GROUPS IN THE PROJECT"));
    }

    private static string[] ManyGroups(int count)
    {
        var groups = new string[count];
        for (int i = 0; i < count; i++)
            groups[i] = $"part{i}";
        return groups;
    }
}

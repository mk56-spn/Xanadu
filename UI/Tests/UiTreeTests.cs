using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Systems.Layout;
using Writer.Systems.Ui;
using Writer.Tests.Support;
using Position = Writer.Ecs.Position;

namespace Writer.Tests.UI;

[TestFixture]
public sealed class UiTreeTests : EcsSystemTest
{
    private Entity _tree;

    private static readonly UiTreeItem[] Items =
    {
        new("root", null, "Root"),
        new("torso", "root", "Torso"),
        new("head", "torso", "Head"),
        new("arm", "torso", "Arm"),
        new("leg", "root", "Leg"),
    };

    protected override void OnEcsSetUp()
    {
        Root.Add(new ContainerLayoutSystem());
        Root.Add(new UiTreeSystem());

        UiBuilder ui = UiBuilder.Begin(Store, new FakeVisualRidFactory()).Layer(0);
        ui.Container(
            new Vector2(300, 300),
            new Vector2(300, 400),
            StackDirection.Vertical,
            cross: StackCrossAlignment.Start);
        _tree = ui.Tree(240);
        ui.SetTreeItems(_tree, Items);
        Frame(3);
    }

    private Entity Row(string id) => UiTreeControl.FindNode(_tree, id);

    private Entity Label(string id) => Row(id).GetComponent<UiTreeNode>().Label;

    private Entity Arrow(string id) => Row(id).GetComponent<UiTreeNode>().Disclosure;

    private void Click(Entity entity)
    {
        entity.AddTag<Pressed>();
        Frame();
        entity.RemoveTag<Pressed>();
        Frame();
    }

    private bool Visible(string id) => !Row(id).Tags.Has<LayoutIgnore>();

    private float Top(string id)
    {
        Entity row = Row(id);
        return UiTransform.WorldPosition(row, row.GetComponent<Position>().Value).Y;
    }

    private float LabelLeft(string id)
    {
        Entity label = Label(id);
        Vector2 center = UiTransform.WorldPosition(label, label.GetComponent<Position>().Value);
        return center.X - label.GetComponent<Rectangle>().Bounds.Size.X / 2;
    }

    private List<string> VisibleIds()
    {
        var ids = new List<string>();
        foreach (Entity row in _tree.ChildEntities)
        {
            if (!row.Tags.Has<LayoutIgnore>())
                ids.Add(row.GetComponent<UiTreeNode>().Id);
        }
        return ids;
    }

    [Test]
    public void RowsAreOrderedDepthFirstWhateverTheInputOrder()
    {
        UiBuilder ui = UiBuilder.Begin(Store, new FakeVisualRidFactory()).Layer(0);
        ui.SetTreeItems(_tree, new UiTreeItem[]
        {
            new("leg", "root", "Leg"),
            new("head", "torso", "Head"),
            new("root", null, "Root"),
            new("torso", "root", "Torso"),
        });
        Frame(2);

        Assert.That(VisibleIds(), Is.EqualTo(new[] { "root", "leg", "torso", "head" }));
    }

    [Test]
    public void ChildrenAreIndentedOneStepPerLevel()
    {
        float step = UiTheme.TreeIndent;

        Assert.That(LabelLeft("torso") - LabelLeft("root"), Is.EqualTo(step).Within(0.01f));
        Assert.That(LabelLeft("head") - LabelLeft("torso"), Is.EqualTo(step).Within(0.01f));
        Assert.That(LabelLeft("arm"), Is.EqualTo(LabelLeft("head")).Within(0.01f));
        Assert.That(LabelLeft("leg"), Is.EqualTo(LabelLeft("torso")).Within(0.01f));
    }

    [Test]
    public void RowsStackTopToBottomInOrder()
    {
        string[] order = { "root", "torso", "head", "arm", "leg" };
        for (int i = 1; i < order.Length; i++)
            Assert.That(Top(order[i]), Is.GreaterThan(Top(order[i - 1])));
    }

    [Test]
    public void OnlyNodesWithChildrenHaveAnArrow()
    {
        Assert.That(Arrow("torso").HasComponent<UiTreeDisclosure>(), Is.True);
        Assert.That(Arrow("head").HasComponent<UiTreeDisclosure>(), Is.False);
        Assert.That(Arrow("torso").GetComponent<Text>().Value, Is.EqualTo("▾"));
    }

    [Test]
    public void CollapsingHidesTheWholeSubtreeAndClosesTheGap()
    {
        float legBefore = Top("leg");

        Click(Arrow("torso"));

        Assert.That(Visible("torso"), Is.True);
        Assert.That(Visible("head"), Is.False);
        Assert.That(Visible("arm"), Is.False);
        Assert.That(Visible("leg"), Is.True);
        Assert.That(Top("leg"), Is.LessThan(legBefore));
        Assert.That(Arrow("torso").GetComponent<Text>().Value, Is.EqualTo("▸"));
    }

    [Test]
    public void CollapsingAnAncestorHidesDeepDescendantsAndExpandingRestoresThem()
    {
        Click(Arrow("root"));
        Assert.That(VisibleIds(), Is.EqualTo(new[] { "root" }));

        Click(Arrow("root"));
        Assert.That(VisibleIds(), Is.EqualTo(new[] { "root", "torso", "head", "arm", "leg" }));
    }

    [Test]
    public void ExpandingAParentKeepsAnInnerCollapsedNodeCollapsed()
    {
        Click(Arrow("torso"));
        Click(Arrow("root"));
        Click(Arrow("root"));

        Assert.That(VisibleIds(), Is.EqualTo(new[] { "root", "torso", "leg" }));
    }

    [Test]
    public void HoldingTheArrowTogglesOnlyOnce()
    {
        Arrow("torso").AddTag<Pressed>();
        Frame(5);

        Assert.That(UiTreeControl.IsExpanded(_tree, "torso"), Is.False);
    }

    [Test]
    public void ClickingALabelSelectsItAndRaisesChangedOnce()
    {
        Click(Label("head"));

        Assert.That(UiTreeControl.Selected(_tree), Is.EqualTo("head"));
        Assert.That(_tree.GetComponent<UiTreeChanged>().Id, Is.EqualTo("head"));
    }

    [Test]
    public void ReselectingTheSameNodeRaisesNothing()
    {
        Click(Label("head"));
        _tree.RemoveComponent<UiTreeChanged>();

        Click(Label("head"));

        Assert.That(_tree.HasComponent<UiTreeChanged>(), Is.False);
    }

    [Test]
    public void OnlyTheSelectedLabelIsHighlighted()
    {
        Click(Label("head"));
        Color selected = Label("head").GetComponent<ButtonVisual>().Normal;
        Color other = Label("arm").GetComponent<ButtonVisual>().Normal;
        Assert.That(selected, Is.EqualTo(UiTheme.Selected));
        Assert.That(other, Is.Not.EqualTo(selected));

        Click(Label("arm"));
        Assert.That(Label("head").GetComponent<ButtonVisual>().Normal, Is.EqualTo(other));
        Assert.That(Label("arm").GetComponent<ButtonVisual>().Normal, Is.EqualTo(selected));
    }

    [Test]
    public void SelectingProgrammaticallyRevealsCollapsedAncestorsWithoutRaisingChanged()
    {
        Click(Arrow("torso"));
        Assert.That(Visible("head"), Is.False);

        UiTreeControl.Select(_tree, "head", raiseChanged: false);
        Frame(2);

        Assert.That(Visible("head"), Is.True);
        Assert.That(UiTreeControl.Selected(_tree), Is.EqualTo("head"));
        Assert.That(_tree.HasComponent<UiTreeChanged>(), Is.False);
    }

    [Test]
    public void RebuildingKeepsCollapsedAndSelectedStateForSurvivingIds()
    {
        Click(Arrow("torso"));
        Click(Label("leg"));
        UiBuilder ui = UiBuilder.Begin(Store, new FakeVisualRidFactory()).Layer(0);

        ui.SetTreeItems(_tree, new UiTreeItem[]
        {
            new("root", null, "Root"),
            new("torso", "root", "Torso"),
            new("head", "torso", "Head"),
            new("leg", "root", "Leg"),
            new("tail", "root", "Tail"),
        });
        Frame(3);

        Assert.That(UiTreeControl.IsExpanded(_tree, "torso"), Is.False);
        Assert.That(UiTreeControl.Selected(_tree), Is.EqualTo("leg"));
        Assert.That(VisibleIds(), Is.EqualTo(new[] { "root", "torso", "leg", "tail" }));
    }

    [Test]
    public void RebuildingForgetsStateOfIdsThatAreGone()
    {
        Click(Arrow("torso"));
        Click(Label("head"));
        UiBuilder ui = UiBuilder.Begin(Store, new FakeVisualRidFactory()).Layer(0);

        ui.SetTreeItems(_tree, new UiTreeItem[] { new("root", null, "Root") });
        Frame(2);

        Assert.That(UiTreeControl.Selected(_tree), Is.EqualTo(""));
        Assert.That(_tree.GetComponent<UiTree>().Collapsed, Is.Empty);
    }

    [Test]
    public void RebuildingDoesNotLeakOldRowsOrTheirChildren()
    {
        UiBuilder ui = UiBuilder.Begin(Store, new FakeVisualRidFactory()).Layer(0);
        ui.SetTreeItems(_tree, Items);
        Frame(2);
        int entities = Store.Count;

        ui.SetTreeItems(_tree, Items);
        ui.SetTreeItems(_tree, Items);
        Frame(2);

        Assert.That(Store.Count, Is.EqualTo(entities));
        Assert.That(Store.Query<UiTreeNode>().Count, Is.EqualTo(Items.Length));
        Assert.That(Store.Query<UiTreeLabel>().Count, Is.EqualTo(Items.Length));
        Assert.That(Store.Query<UiTreeDisclosure>().Count, Is.EqualTo(2));
    }

    [Test]
    public void ItemsWithAMissingParentBecomeRoots()
    {
        UiBuilder ui = UiBuilder.Begin(Store, new FakeVisualRidFactory()).Layer(0);

        ui.SetTreeItems(_tree, new UiTreeItem[] { new("orphan", "nobody", "Orphan") });
        Frame(2);

        Assert.That(Row("orphan").GetComponent<UiTreeNode>().Depth, Is.EqualTo(0));
    }

    [Test]
    public void ParentCyclesAndDuplicateIdsAreHandled()
    {
        UiBuilder ui = UiBuilder.Begin(Store, new FakeVisualRidFactory()).Layer(0);

        Assert.DoesNotThrow(() => ui.SetTreeItems(_tree, new UiTreeItem[]
        {
            new("a", "b", "A"),
            new("b", "a", "B"),
        }));
        Assert.Throws<System.ArgumentException>(() => ui.SetTreeItems(_tree, new UiTreeItem[]
        {
            new("a", null, "A"),
            new("a", null, "A again"),
        }));
    }
}

using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Input;
using Xanadu.Systems.Layout;
using Xanadu.Systems.Ui;
using Xanadu.Tests.Support;
using Xanadu.Ui.Systems.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.UI;

[TestFixture]
public sealed class UiTreeOverlaySystemTests : EcsSystemTest
{
    private ScriptedToggle _toggle = null!;

    protected override void OnEcsSetUp()
    {
        _toggle = new ScriptedToggle();
        Root.Add(new ContainerLayoutSystem());
        Root.Add(new UiTreeSystem());

        // The panel deletes rows, so like in the game it plays back separately from layout.
        var debug = new Friflo.Engine.ECS.Systems.SystemGroup("Debug");
        debug.Add(new UiTreeOverlaySystem(_toggle, new FakeVisualRidFactory()));
        Root.Add(debug);
    }

    private Entity Panel(Entity? parent = null, Vector2? position = null)
    {
        Entity panel = Store.CreateEntity(
            new Position(position ?? Vector2.Zero),
            new Rectangle(new Rect2(0, 0, 100, 100)),
            new UiContainer(StackDirection.Vertical));
        parent?.AddChild(panel);
        return panel;
    }

    private Entity Button(string label, Entity parent)
    {
        Entity button = Store.CreateEntity(
            new Position(Vector2.Zero),
            new Rectangle(new Rect2(0, 0, 50, 20)),
            new Text(label),
            new ButtonVisual(Colors.Red, Colors.Red, Colors.White));
        parent.AddChild(button);
        return button;
    }

    private void Open()
    {
        _toggle.Pressed = true;
        Frame();
        _toggle.Pressed = false;
    }

    private Entity Tree => Store.Query<UiTree>().Entities.First();

    private Entity Row(Entity listed) => UiTreeControl.FindNode(Tree, listed.Id.ToString());

    private int RowCount => UiTreeControl.Nodes(Tree).Count();

    [Test]
    public void TreeSitsInAScrollView()
    {
        Panel();
        Open();

        Entity content = Store.Query<ScrollView>().Entities.Single().GetComponent<ScrollView>().Content;
        Assert.That(Tree.Parent, Is.EqualTo(content));
        Assert.That(Store.Query<ScrollView>().Entities.Single().GetComponent<UiContainer>().Clip, Is.True);
    }

    [Test]
    public void ChildrenNestUnderTheirParent()
    {
        Entity outer = Panel();
        Entity inner = Panel(outer);
        Entity play = Button("PLAY", inner);
        Entity quit = Button("QUIT", outer);

        Open();

        Assert.That(RowCount, Is.EqualTo(4));
        Assert.That(UiTreeControl.ParentNode(Row(outer)).IsNull, Is.True);
        Assert.That(UiTreeControl.ParentNode(Row(play)), Is.EqualTo(Row(inner)));
        Assert.That(UiTreeControl.ParentNode(Row(quit)), Is.EqualTo(Row(outer)));
        Assert.That(UiTreeControl.Label(Row(play)).GetComponent<Text>().Value, Does.StartWith("PLAY"));
    }

    [Test]
    public void PlainEntitiesDoNotAddDepth()
    {
        Entity outer = Panel();
        Entity plain = Store.CreateEntity(
            new Position(Vector2.Zero),
            new Rectangle(new Rect2(0, 0, 10, 10)));
        outer.AddChild(plain);
        Entity nested = Button("NESTED", plain);

        Open();

        Assert.That(RowCount, Is.EqualTo(2));
        Assert.That(UiTreeControl.ParentNode(Row(nested)), Is.EqualTo(Row(outer)));
    }

    [Test]
    public void HiddenSubtreesAreLeftOut()
    {
        Entity outer = Panel();
        Entity hidden = Panel(outer);
        UiLayout.SetHidden(hidden, true);
        Entity gone = Button("GONE", hidden);
        Entity here = Button("HERE", outer);

        Open();

        Assert.That(Row(gone).IsNull, Is.True);
        Assert.That(Row(here).IsNull, Is.False);
    }

    [Test]
    public void PanelDoesNotListItself()
    {
        Panel();
        Open();
        Frame(3);

        Assert.That(RowCount, Is.EqualTo(1));
    }

    [Test]
    public void UnchangedListingKeepsItsRowsAndCollapse()
    {
        Entity outer = Panel();
        Button("PLAY", outer);
        Open();
        Entity row = Row(outer);

        UiTreeControl.SetExpanded(Tree, outer.Id.ToString(), false);
        Frame(3);

        Assert.That(Row(outer), Is.EqualTo(row));
        Assert.That(UiTreeControl.IsExpanded(Tree, outer.Id.ToString()), Is.False);
    }

    [Test]
    public void ChangesInTheUiReachTheTree()
    {
        Entity outer = Panel();
        Open();

        Entity added = Button("NEW", outer);
        Frame();

        Assert.That(Row(added).IsNull, Is.False);
    }

    [Test]
    public void ClosingRemovesThePanel()
    {
        Panel();
        Open();
        Frame();
        Open();

        Assert.That(Store.Query<UiTree>().Count, Is.EqualTo(0));
        Assert.That(Store.Query<ScrollView>().Count, Is.EqualTo(0));
    }

    [Test]
    public void ClickingARowTagsItsEntityOnly()
    {
        Entity outer = Panel();
        Entity play = Button("PLAY", outer);
        Entity quit = Button("QUIT", outer);
        Open();

        Click(UiTreeControl.Label(Row(play)));
        Assert.That(play.Tags.Has<UiTreeOverlaySelected>(), Is.True);

        Click(UiTreeControl.Label(Row(quit)));
        Assert.That(play.Tags.Has<UiTreeOverlaySelected>(), Is.False);
        Assert.That(quit.Tags.Has<UiTreeOverlaySelected>(), Is.True);
        Assert.That(Tree.HasComponent<UiTreeChanged>(), Is.False);
    }

    [Test]
    public void ClosingAndLosingTheRowClearTheMark()
    {
        Entity outer = Panel();
        Entity play = Button("PLAY", outer);
        Open();
        Click(UiTreeControl.Label(Row(play)));

        UiLayout.SetHidden(play, true);
        Frame();
        Assert.That(play.Tags.Has<UiTreeOverlaySelected>(), Is.False);

        UiLayout.SetHidden(play, false);
        Frame();
        Click(UiTreeControl.Label(Row(play)));
        Open();
        Assert.That(play.Tags.Has<UiTreeOverlaySelected>(), Is.False);
    }

    private void Click(Entity entity)
    {
        entity.AddTag<Pressed>();
        Frame();
        entity.RemoveTag<Pressed>();
        Frame();
    }

    private sealed class ScriptedToggle : IOverlayToggleSource
    {
        public bool Pressed;
        public bool IsTogglePressed() => Pressed;
    }
}

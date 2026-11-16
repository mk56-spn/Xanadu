using Xanadu.Tests.Support.Fakes;
using System.Linq;
using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Tests.Support;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Input;
using Xanadu.Ui.Systems.Layout;
using Xanadu.Ui.Systems.Ui;
using Position = Xanadu.Core.Position;

namespace Xanadu.Ui.Tests;

[TestFixture]
public sealed class UiTreeOverlaySystemTests : EcsSystemTest
{
    private ScriptedToggle _toggle = null!;
    private ScriptedPointer _pointer = null!;
    private UiTreeOverlaySystem _tree = null!;

    protected override void OnEcsSetUp()
    {
        _toggle = new ScriptedToggle();
        _pointer = new ScriptedPointer();
        Root.Add(new ContainerLayoutSystem());
        Root.Add(new ScrollSystem(_pointer));

        // The panel deletes its rows on close, so like in the game it plays back separately from layout.
        var debug = new Friflo.Engine.ECS.Systems.SystemGroup("Debug");
        debug.Add(_tree = new UiTreeOverlaySystem(_toggle, new FakeVisualRidFactory(), _pointer));
        Root.Add(debug);
    }

    private Entity Panel(Entity? parent = null, Vector2? position = null)
    {
        Entity panel = Store.CreateEntity(
            new Position(position ?? new Vector2(200, 200)),
            new Rectangle(new Rect2(-100, -100, 200, 200)),
            new Drawable(0),
            new UiContainer(StackDirection.Vertical));
        parent?.AddChild(panel);
        return panel;
    }

    private Entity Button(string label, Entity parent)
    {
        Entity button = Store.CreateEntity(
            new Position(Vector2.Zero),
            new Rectangle(new Rect2(-25, -10, 50, 20)),
            new Drawable(0),
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
        Frame(2);
    }

    private void Press(Entity entity)
    {
        entity.AddTag<Pressed>();
        Frame();
        entity.RemoveTag<Pressed>();
        Frame();
    }

    private Entity Command(UiTreeCommand id, int arg = 0) =>
        Store.Query<UiCommand>().Entities.First(e => e.GetComponent<UiCommand>().Is(id) && e.GetComponent<UiCommand>().Arg == arg);

    /// <summary>The tree's row names on screen, top to bottom.</summary>
    private string[] Rows() =>
        Store.Query<UiCommand>().Entities
            .Where(e => e.GetComponent<UiCommand>().Is(UiTreeCommand.Inspect) && !UiLayout.IsHidden(e.Parent))
            .OrderBy(e => e.GetComponent<UiCommand>().Arg)
            .Select(e => e.GetComponent<Text>().Value)
            .Where(text => text.Length > 0)
            .ToArray();

    private Entity NameRow(string startsWith) =>
        Store.Query<UiCommand>().Entities.First(e =>
            e.GetComponent<UiCommand>().Is(UiTreeCommand.Inspect) && e.GetComponent<Text>().Value.StartsWith(startsWith));

    private string[] Props() =>
        Store.Query<Text>().Entities.Select(e => e.GetComponent<Text>().Value).ToArray();

    [Test]
    public void OpensPickingAndOutlinesWhatTheCursorIsOn()
    {
        Entity outer = Panel();
        Entity play = Button("PLAY", outer);
        _pointer.Current = new PointerInput(new Vector2(200, 200), false);

        Open();

        Assert.That(_tree.Picking, Is.True);
        Assert.That(play.Tags.Has<UiTreeOverlayHovered>(), Is.True, "the button draws, so it wins over the container round it");
        Assert.That(outer.Tags.Has<UiTreeOverlayHovered>(), Is.False);
    }

    [Test]
    public void ClickingInPickModeOpensTheTreeOnItAndInspectsIt()
    {
        Entity outer = Panel();
        Entity play = Button("PLAY", outer);
        _pointer.Current = new PointerInput(new Vector2(200, 200), false);
        Open();

        Press(Command(UiTreeCommand.PickHere));

        Assert.That(_tree.Picking, Is.False);
        Assert.That(_tree.Root, Is.EqualTo(outer));
        Assert.That(_tree.Inspected, Is.EqualTo(play));
        Assert.That(play.Tags.Has<UiTreeOverlaySelected>(), Is.True);
        Assert.That(Rows(), Has.Some.StartsWith("COLUMN").And.Some.StartsWith("BUTTON  \"PLAY\""));
    }

    [Test]
    public void TheCatcherKeepsThePickedButtonFromBeingPressed()
    {
        Panel();
        Open();

        Entity catcher = Command(UiTreeCommand.PickHere);
        Assert.That(UiLayout.IsHidden(catcher), Is.False);
        Assert.That(catcher.Tags.Has<TakesInput>(), Is.True);
    }

    [Test]
    public void EveryRootIsListedUntilSomethingIsPicked()
    {
        Panel();
        Store.CreateEntity(new Position(new Vector2(10, 20)));
        Open();

        Assert.That(Rows().Length, Is.EqualTo(2));
        Assert.That(Rows(), Has.Some.StartsWith("ENTITY"));
    }

    [Test]
    public void ANameInspectsAndAgainMakesItTheRoot()
    {
        Entity outer = Panel();
        Entity inner = Panel(outer);
        Button("PLAY", inner);
        _pointer.Current = new PointerInput(new Vector2(200, 200), false);
        Open();
        Press(Command(UiTreeCommand.PickHere));

        Press(NameRow("COLUMN  #" + inner.Id));
        Assert.That(_tree.Inspected, Is.EqualTo(inner));
        Assert.That(_tree.Root, Is.EqualTo(inner.Parent));

        Press(NameRow("COLUMN  #" + inner.Id));
        Assert.That(_tree.Root, Is.EqualTo(inner));

        Press(Command(UiTreeCommand.Up));
        Assert.That(_tree.Root, Is.EqualTo(outer));
    }

    [Test]
    public void TheArrowFoldsABranch()
    {
        Entity outer = Panel();
        Button("PLAY", outer);
        _pointer.Current = new PointerInput(new Vector2(200, 200), false);
        Open();
        Press(Command(UiTreeCommand.PickHere));
        Assert.That(Rows().Length, Is.EqualTo(2));

        Press(Command(UiTreeCommand.Toggle, 0));

        Assert.That(Rows().Length, Is.EqualTo(1));
        Assert.That(Rows()[0], Does.EndWith("(1)"));
    }

    [Test]
    public void RowsStayTheSameEntitiesWhileWhatTheyNameChanges()
    {
        Entity outer = Panel();
        Entity play = Button("PLAY", outer);
        _pointer.Current = new PointerInput(new Vector2(200, 200), false);
        Open();
        Press(Command(UiTreeCommand.PickHere));
        Entity row = NameRow("BUTTON  \"PLAY\"");

        play.GetComponent<Text>().Value = "STOP";
        Frame();

        Assert.That(NameRow("BUTTON  \"STOP\""), Is.EqualTo(row));
    }

    [Test]
    public void TheInspectorListsComponentsAndTheirFields()
    {
        Entity outer = Panel();
        Button("PLAY", outer);
        _pointer.Current = new PointerInput(new Vector2(200, 200), false);
        Open();
        Press(Command(UiTreeCommand.PickHere));

        string[] texts = Props();
        Assert.That(texts, Does.Contain("▪ ButtonVisual"));
        Assert.That(texts, Does.Contain("\"PLAY\""));
    }

    [Test]
    public void SearchListsMatchesFromAnywhereUnderTheRoot()
    {
        Entity outer = Panel();
        Entity inner = Panel(outer);
        Button("PLAY", inner);
        Button("QUIT", outer);
        Open();

        Entity search = Store.Query<TextField>().Entities.Single();
        search.GetComponent<TextField>().Value = "quit";
        Frame();

        Assert.That(Rows(), Has.Length.EqualTo(1));
        Assert.That(Rows()[0], Does.StartWith("BUTTON  \"QUIT\""));
    }

    [Test]
    public void DebugPanelsAreNeitherListedNorPicked()
    {
        Entity debug = Panel();
        debug.AddTag<DebugUi>();
        Panel(position: new Vector2(600, 600));
        _pointer.Current = new PointerInput(new Vector2(200, 200), false);
        Open();

        Assert.That(Rows(), Has.Length.EqualTo(1));
        Assert.That(debug.Tags.Has<UiTreeOverlayHovered>(), Is.False);
    }

    [Test]
    public void ClosingRemovesThePanelAndItsMarks()
    {
        Entity outer = Panel();
        Entity play = Button("PLAY", outer);
        _pointer.Current = new PointerInput(new Vector2(200, 200), false);
        Open();
        Press(Command(UiTreeCommand.PickHere));

        Open();

        Assert.That(Store.Query<UiList>().Count, Is.EqualTo(0));
        Assert.That(play.Tags.Has<UiTreeOverlaySelected>(), Is.False);
        Assert.That(play.Tags.Has<UiTreeOverlayHovered>(), Is.False);
        Assert.That(Store.Query<UiCommand>().Count, Is.EqualTo(0));
    }

    [Test]
    public void DeletingTheInspectedEntityLetsGoOfIt()
    {
        Entity outer = Panel();
        Entity play = Button("PLAY", outer);
        _pointer.Current = new PointerInput(new Vector2(200, 200), false);
        Open();
        Press(Command(UiTreeCommand.PickHere));

        play.DeleteEntity();
        Assert.DoesNotThrow(() => Frame(2));
        Assert.That(Rows(), Has.Length.EqualTo(1));
    }

    private sealed class ScriptedPointer : IPointerInputSource
    {
        public PointerInput Current = new(new Vector2(-1000, -1000), false);
        public PointerInput Read() => Current;
    }

    private sealed class ScriptedToggle : IOverlayToggleSource
    {
        public bool Pressed;
        public bool IsTogglePressed() => Pressed;
    }
}

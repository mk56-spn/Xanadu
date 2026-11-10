using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Input;
using Xanadu.Systems.Ui;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.UI;

[TestFixture]
public sealed class UiDropdownSystemTests
{
    private static readonly Vector2 ListSize = new(100, 80);

    private EntityStore _store = null!;
    private SystemRoot _root = null!;
    private float _time;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _root = new SystemRoot(_store);
        _root.Add(new UiDropdownSystem(_store));
        _time = 0f;
    }

    [Test]
    public void OpensAndLaysOutOptions()
    {
        Entity button = Button(new Vector2(100, 50));
        Entity list = List();
        button.AddComponent(new Dropdown(list, 0, ListSize));
        Entity option = Option(button, 0);

        Press(button);

        Assert.That(button.GetComponent<Dropdown>().Open, Is.True);
        Assert.That(list.GetComponent<Rectangle>().Bounds.Size, Is.EqualTo(ListSize));
        Assert.That(option.GetComponent<Position>().Value.Y, Is.GreaterThan(0));
    }

    [Test]
    public void NonExclusiveDropdownsStayOpenTogether()
    {
        Entity first = Button(new Vector2(100, 50));
        first.AddComponent(new Dropdown(List(), 0, closeOthers: false));
        Entity second = Button(new Vector2(100, 150));
        second.AddComponent(new Dropdown(List(), 0, closeOthers: false));

        Press(first);
        Press(second);

        Assert.That(first.GetComponent<Dropdown>().Open, Is.True);
        Assert.That(second.GetComponent<Dropdown>().Open, Is.True);
    }

    [Test]
    public void OptionsLayOutInValueOrder()
    {
        Entity button = Button(new Vector2(100, 50));
        button.AddComponent(new Dropdown(List(), 0, ListSize));
        Entity third = Option(button, 2);
        Entity first = Option(button, 0);

        Press(button);

        Assert.That(first.GetComponent<Position>().Value.Y,
            Is.LessThan(third.GetComponent<Position>().Value.Y));
    }

    [Test]
    public void OpeningOneDropdownClosesTheOther()
    {
        Entity first = Button(new Vector2(100, 50));
        Entity firstList = List();
        first.AddComponent(new Dropdown(firstList, 0));
        Entity second = Button(new Vector2(100, 150));
        second.AddComponent(new Dropdown(List(), 0));

        Press(first);
        Assert.That(first.GetComponent<Dropdown>().Open, Is.True);

        Press(second);

        Assert.That(first.GetComponent<Dropdown>().Open, Is.False);
        Assert.That(UiLayout.IsHidden(firstList), Is.True);
        Assert.That(second.GetComponent<Dropdown>().Open, Is.True);
    }

    [Test]
    public void OpenListFollowsTheButtonWhenLayoutMovesIt()
    {
        Entity button = Button(new Vector2(100, 50));
        Entity list = List();
        button.AddComponent(new Dropdown(list, 0, ListSize));

        Press(button);
        float first = list.GetComponent<Position>().Value.Y;

        button.GetComponent<Position>().Value = new Vector2(100, 150);
        Step();

        Assert.That(list.GetComponent<Position>().Value.Y, Is.EqualTo(first + 100f));
    }

    [Test]
    public void AccountsForAParentedButtonTransform()
    {
        Entity parent = _store.CreateEntity(new Position(new Vector2(100, 50)));
        Entity button = Button(new Vector2(800, 200));
        parent.AddChild(button);
        Entity list = List();
        button.AddComponent(new Dropdown(list, 0, ListSize));

        Press(button);

        Assert.That(list.GetComponent<Position>().Value, Is.EqualTo(new Vector2(900, 314)));
    }

    [Test]
    public void SelectingAnOptionClosesTheDropdown()
    {
        Entity button = Button(new Vector2(100, 50));
        button.AddComponent(new Dropdown(List(), 0));
        Entity option = Option(button, 2);

        Press(button);
        Press(option);

        Assert.That(button.GetComponent<Dropdown>().Open, Is.False);
        Assert.That(button.GetComponent<Dropdown>().Selected, Is.EqualTo(2));
        Assert.That(UiLayout.IsHidden(option), Is.True);
    }

    private Entity Button(Vector2 center) =>
        _store.CreateEntity(new Position(center), new Rectangle(new Rect2(-50, -20, 100, 40)));

    private Entity List() =>
        _store.CreateEntity(new Position(Vector2.Zero), new Rectangle(new Rect2(0, 0, 0, 0)));

    private Entity Option(Entity button, int value)
    {
        Entity option = _store.CreateEntity(
            new Position(Vector2.Zero),
            new Rectangle(new Rect2(-40, -12, 80, 24)));
        option.AddComponent(new DropdownOption(button, value));
        return option;
    }

    /// <summary>Holds the press for one update, then lets go.</summary>
    private void Press(Entity control)
    {
        control.AddTag<Pressed>();
        Step();
        control.RemoveTag<Pressed>();
    }

    private void Step()
    {
        _time += 1f;
        _root.Update(new UpdateTick(1f / 60f, _time));
    }
}

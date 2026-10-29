using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Ui;
using Xanadu.Tests.Support;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.UI;

[TestFixture]
public sealed class UiTabSystemTests
{
    private EntityStore _store = null!;
    private SystemRoot _root = null!;
    private Entity[] _buttons = null!;
    private Entity[] _bodies = null!;
    private Entity _strip;

    private static readonly Vector2 Slot = new(500, 600);

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _buttons = new Entity[2];
        _bodies = new Entity[2];
        for (int i = 0; i < 2; i++)
        {
            _buttons[i] = _store.CreateEntity(
                ButtonVisual.Create(Colors.Gray),
                new FillColor(Colors.Gray));
            _bodies[i] = _store.CreateEntity(new Position(UiLayout.Hidden));
        }

        _strip = _store.CreateEntity(new UiTabs(_buttons, _bodies, Slot));
        _root = new SystemRoot(_store);
        _root.Add(new UiTabSystem());
    }

    private void Frame() => _root.Update(new UpdateTick(1f / 60f, 0));

    private void Click(int index)
    {
        _buttons[index].AddTag<Pressed>();
        Frame();
        _buttons[index].RemoveTag<Pressed>();
        Frame();
    }

    [Test]
    public void TheFirstTabShowsItsBodyAndParksTheRest()
    {
        Frame();

        Assert.That(_bodies[0].GetComponent<Position>().Value, Is.EqualTo(Slot));
        Assert.That(_bodies[1].GetComponent<Position>().Value, Is.EqualTo(UiLayout.Hidden));
    }

    [Test]
    public void PressingATabSwapsWhichBodyIsShown()
    {
        Frame();
        Click(1);

        Assert.That(_strip.GetComponent<UiTabs>().Selected, Is.EqualTo(1));
        Assert.That(_bodies[1].GetComponent<Position>().Value, Is.EqualTo(Slot));
        Assert.That(_bodies[0].GetComponent<Position>().Value, Is.EqualTo(UiLayout.Hidden));
    }

    [Test]
    public void HoldingATabSelectsItOnlyOnce()
    {
        Frame();
        _buttons[1].AddTag<Pressed>();
        Frame();
        _strip.GetComponent<UiTabs>().Selected = 0;
        Frame();

        Assert.That(_strip.GetComponent<UiTabs>().Selected, Is.EqualTo(0));
    }

    [Test]
    public void TheSelectedButtonIsLit()
    {
        Frame();
        Click(1);

        Assert.That(_buttons[1].GetComponent<ButtonVisual>().Normal, Is.EqualTo(_strip.GetComponent<UiTabs>().OnColor));
        Assert.That(_buttons[0].GetComponent<ButtonVisual>().Normal, Is.EqualTo(_strip.GetComponent<UiTabs>().OffColor));
    }
}

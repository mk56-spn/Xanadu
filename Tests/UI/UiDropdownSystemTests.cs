using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Systems.Input;
using Writer.Systems.Ui;
using Position = Writer.Ecs.Position;

namespace Writer.Tests.UI;

[TestFixture]
public sealed class UiDropdownSystemTests
{
    [Test]
    public void OpensAndLaysOutOptions()
    {
        EntityStore store = new();
        var root = new SystemRoot(store);
        root.Add(new UiDropdownSystem(store));

        Entity button = store.CreateEntity(
            new Position(new Vector2(100, 50)),
            new Rectangle(new Rect2(-50, -20, 100, 40)));
        Entity list = store.CreateEntity(
            new Position(new Vector2(-10_000, -10_000)),
            new Rectangle(new Rect2(0, 0, 0, 0)));
        button.AddComponent(new Dropdown(list, 0, new Vector2(100, 80)));

        Entity option = store.CreateEntity(
            new Position(new Vector2(-10_000, -10_000)),
            new Rectangle(new Rect2(-40, -12, 80, 24)));
        option.AddComponent(new DropdownOption(button, 0));

        button.AddTag<Pressed>();
        root.Update(new UpdateTick(1f / 60f, 1f));

        Assert.That(button.GetComponent<Dropdown>().Open, Is.True);
        Assert.That(list.GetComponent<Rectangle>().Bounds.Size,
            Is.EqualTo(new Vector2(100, 80)));
        Assert.That(option.GetComponent<Position>().Value.Y,
            Is.GreaterThan(0));
    }

    [Test]
    public void OptionsLayOutInValueOrder()
    {
        EntityStore store = new();
        var root = new SystemRoot(store);
        root.Add(new UiDropdownSystem(store));

        Entity button = store.CreateEntity(
            new Position(new Vector2(100, 50)),
            new Rectangle(new Rect2(-50, -20, 100, 40)));
        Entity list = store.CreateEntity(
            new Position(new Vector2(-10_000, -10_000)),
            new Rectangle(new Rect2(0, 0, 0, 0)));
        button.AddComponent(new Dropdown(list, 0, new Vector2(100, 80)));

        Entity third = store.CreateEntity(
            new Position(new Vector2(-10_000, -10_000)),
            new Rectangle(new Rect2(-40, -12, 80, 24)));
        third.AddComponent(new DropdownOption(button, 2));
        Entity first = store.CreateEntity(
            new Position(new Vector2(-10_000, -10_000)),
            new Rectangle(new Rect2(-40, -12, 80, 24)));
        first.AddComponent(new DropdownOption(button, 0));

        button.AddTag<Pressed>();
        root.Update(new UpdateTick(1f / 60f, 1f));

        Assert.That(first.GetComponent<Position>().Value.Y,
            Is.LessThan(third.GetComponent<Position>().Value.Y));
    }

    [Test]
    public void OpeningOneDropdownClosesTheOther()
    {
        EntityStore store = new();
        var root = new SystemRoot(store);
        root.Add(new UiDropdownSystem(store));

        Entity first = store.CreateEntity(
            new Position(new Vector2(100, 50)),
            new Rectangle(new Rect2(-50, -20, 100, 40)));
        Entity firstList = store.CreateEntity(
            new Position(new Vector2(-10_000, -10_000)),
            new Rectangle(new Rect2(0, 0, 0, 0)));
        first.AddComponent(new Dropdown(firstList, 0));

        Entity second = store.CreateEntity(
            new Position(new Vector2(100, 150)),
            new Rectangle(new Rect2(-50, -20, 100, 40)));
        Entity secondList = store.CreateEntity(
            new Position(new Vector2(-10_000, -10_000)),
            new Rectangle(new Rect2(0, 0, 0, 0)));
        second.AddComponent(new Dropdown(secondList, 0));

        first.AddTag<Pressed>();
        root.Update(new UpdateTick(1f / 60f, 1f));
        first.RemoveTag<Pressed>();
        Assert.That(first.GetComponent<Dropdown>().Open, Is.True);

        second.AddTag<Pressed>();
        root.Update(new UpdateTick(1f / 60f, 2f));

        Assert.That(first.GetComponent<Dropdown>().Open, Is.False);
        Assert.That(firstList.GetComponent<Position>().Value,
            Is.EqualTo(new Vector2(-10_000, -10_000)));
        Assert.That(second.GetComponent<Dropdown>().Open, Is.True);
    }

    [Test]
    public void OpenListFollowsTheButtonWhenLayoutMovesIt()
    {
        EntityStore store = new();
        var root = new SystemRoot(store);
        root.Add(new UiDropdownSystem(store));

        Entity button = store.CreateEntity(
            new Position(new Vector2(100, 50)),
            new Rectangle(new Rect2(-50, -20, 100, 40)));
        Entity list = store.CreateEntity(
            new Position(new Vector2(-10_000, -10_000)),
            new Rectangle(new Rect2(0, 0, 0, 0)));
        button.AddComponent(new Dropdown(list, 0, new Vector2(100, 80)));

        button.AddTag<Pressed>();
        root.Update(new UpdateTick(1f / 60f, 1f));
        button.RemoveTag<Pressed>();
        float first = list.GetComponent<Position>().Value.Y;

        button.GetComponent<Position>().Value = new Vector2(100, 150);
        root.Update(new UpdateTick(1f / 60f, 2f));

        Assert.That(list.GetComponent<Position>().Value.Y,
            Is.EqualTo(first + 100f));
    }

    [Test]
    public void AccountsForAParentedButtonTransform()
    {
        EntityStore store = new();
        var root = new SystemRoot(store);
        root.Add(new UiDropdownSystem(store));

        Entity parent = store.CreateEntity(
            new Position(new Vector2(100, 50)));
        Entity button = store.CreateEntity(
            new Position(new Vector2(800, 200)),
            new Rectangle(new Rect2(-50, -20, 100, 40)));
        parent.AddChild(button);
        Entity list = store.CreateEntity(
            new Position(new Vector2(-10_000, -10_000)),
            new Rectangle(new Rect2(0, 0, 0, 0)));
        button.AddComponent(new Dropdown(list, 0, new Vector2(100, 80)));

        button.AddTag<Pressed>();
        root.Update(new UpdateTick(1f / 60f, 1f));

        Assert.That(list.GetComponent<Position>().Value,
            Is.EqualTo(new Vector2(900, 314)));
    }

    [Test]
    public void SelectingAnOptionClosesTheDropdown()
    {
        EntityStore store = new();
        var root = new SystemRoot(store);
        root.Add(new UiDropdownSystem(store));

        Entity button = store.CreateEntity(
            new Position(new Vector2(100, 50)),
            new Rectangle(new Rect2(-50, -20, 100, 40)));
        Entity list = store.CreateEntity(
            new Position(new Vector2(-10_000, -10_000)),
            new Rectangle(new Rect2(0, 0, 0, 0)));
        button.AddComponent(new Dropdown(list, 0));
        Entity option = store.CreateEntity(
            new Position(new Vector2(-10_000, -10_000)),
            new Rectangle(new Rect2(-40, -12, 80, 24)));
        option.AddComponent(new DropdownOption(button, 2));

        button.AddTag<Pressed>();
        root.Update(new UpdateTick(1f / 60f, 1f));
        button.RemoveTag<Pressed>();

        option.AddTag<Pressed>();
        root.Update(new UpdateTick(1f / 60f, 2f));

        Assert.That(button.GetComponent<Dropdown>().Open, Is.False);
        Assert.That(button.GetComponent<Dropdown>().Selected, Is.EqualTo(2));
        Assert.That(option.GetComponent<Position>().Value,
            Is.EqualTo(new Vector2(-10_000, -10_000)));
    }
}

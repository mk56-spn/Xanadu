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

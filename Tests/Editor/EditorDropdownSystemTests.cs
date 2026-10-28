using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Editor.Commands;
using Xanadu.Settings;
using Xanadu.Systems.Editor;
using Xanadu.Systems.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class EditorDropdownSystemTests
{
    [Test]
    public void OpensAndSelectsASnapValue()
    {
        var store = new EntityStore();
        var commands = new EditorCommandBus();
        Entity editor = store.CreateEntity(
            new Playhead(0, 120, 0, false),
            new EditorNoteTarget(KeybindAction.M));

        Entity list = store.CreateEntity(
            new Position(UiLayout.Hidden),
            new Rectangle(new Rect2(-95, -48, 190, 96)));

        Entity button = store.CreateEntity(
            new Position(new Vector2(800, 200)),
            new Rectangle(new Rect2(-95, -17, 190, 34)),
            new Text(""),
            new Dropdown(list, 0),
            new EditorDropdownControl(EditorDropdownKind.Snap));

        Entity option = store.CreateEntity(
            new Position(new Vector2(0, 0)),
            new Rectangle(new Rect2(-92, -14, 184, 28)),
            new DropdownOption(button, 2));

        var root = new SystemRoot(store);
        root.Add(new EditorDropdownSystem(store, commands));
        root.Add(new UiDropdownSystem(store));

        button.AddTag<Pressed>();
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(button.GetComponent<Dropdown>().Open, Is.True);
        Assert.That(
            list.GetComponent<Position>().Value,
            Is.Not.EqualTo(UiLayout.Hidden));
        Assert.That(
            option.GetComponent<Position>().Value,
            Is.EqualTo(new Vector2(800, 237)));
        Assert.That(
            list.GetComponent<Rectangle>().Bounds.Size,
            Is.EqualTo(new Vector2(190, 96)));

        button.RemoveTag<Pressed>();
        root.Update(new UpdateTick(1f / 60f, 0));

        option.AddTag<Pressed>();
        root.Update(new UpdateTick(1f / 60f, 0));
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(button.GetComponent<Dropdown>().Selected, Is.EqualTo(2));
        Assert.That(button.GetComponent<Dropdown>().Open, Is.False);
        Assert.That(
            list.GetComponent<Rectangle>().Bounds.Size,
            Is.EqualTo(Vector2.Zero));
        Assert.That(commands.TryDequeue(out EditorCommand command), Is.True);
        Assert.That(command.Type, Is.EqualTo(EditorCommandType.SetSnap));
        Assert.That(command.IntValue, Is.EqualTo(16));
    }

    [Test]
    public void LinkComponentsCleanUpWhenListsAndOwnersAreDeleted()
    {
        var store = new EntityStore();
        Entity list = store.CreateEntity();
        Entity button = store.CreateEntity(new Dropdown(list, 0));
        Entity option = store.CreateEntity(
            new Position(Vector2.Zero),
            new DropdownOption(button, 1));

        list.DeleteEntity();
        button.DeleteEntity();

        Assert.That(button.IsNull || !button.HasComponent<Dropdown>(), Is.True);
        Assert.That(
            option.IsNull || !option.HasComponent<DropdownOption>(),
            Is.True);
    }

    [Test]
    public void PressingASwatchSetsTheNoteTypeInNoteModeOnly()
    {
        var store = new EntityStore();
        var commands = new EditorCommandBus();
        store.CreateEntity(
            new EditorPlacementModeState(EditorPlacementMode.Notes),
            new EditorNoteTarget(KeybindAction.M));
        Vector2 home = new(100, 400);
        Entity swatch = store.CreateEntity(
            new Position(UiLayout.Hidden),
            new EditorNoteSwatch(KeybindAction.R3, home));

        var root = new SystemRoot(store);
        root.Add(new EditorNoteTypeBarSystem(store, commands));

        root.Update(new UpdateTick(1f / 60f, 0));
        Assert.That(swatch.GetComponent<Position>().Value, Is.EqualTo(home));

        swatch.AddTag<Pressed>();
        root.Update(new UpdateTick(1f / 60f, 0));
        Assert.That(commands.TryDequeue(out EditorCommand command), Is.True);
        Assert.That(command.Type, Is.EqualTo(EditorCommandType.SetTarget));
        Assert.That(command.IntValue, Is.EqualTo((int)KeybindAction.R3));

        store.Query<EditorPlacementModeState>().ToEntityList()[0]
            .GetComponent<EditorPlacementModeState>().Mode = EditorPlacementMode.Mines;
        root.Update(new UpdateTick(1f / 60f, 0));
        Assert.That(swatch.GetComponent<Position>().Value, Is.EqualTo(UiLayout.Hidden));
    }
}

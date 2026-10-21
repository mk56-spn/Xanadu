using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Editor.Commands;
using Writer.Settings;
using Writer.Systems.Editor;
using Position = Writer.Ecs.Position;

namespace Writer.Tests.Editor;

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
            new Position(new Vector2(-10_000, -10_000)),
            new Rectangle(new Rect2(-95, -48, 190, 96)));

        Entity button = store.CreateEntity(
            new Position(new Vector2(800, 200)),
            new Rectangle(new Rect2(-95, -17, 190, 34)),
            new Text(""),
            new EditorDropdown(list, EditorDropdownKind.Snap, 0));

        Entity option = store.CreateEntity(
            new Position(new Vector2(0, 0)),
            new Rectangle(new Rect2(-92, -14, 184, 28)),
            new EditorDropdownOption(button, 2));

        var root = new SystemRoot(store);
        root.Add(new EditorDropdownSystem(store, commands));

        button.AddTag<Pressed>();
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(button.GetComponent<EditorDropdown>().Open, Is.True);
        Assert.That(
            list.GetComponent<Position>().Value,
            Is.Not.EqualTo(new Vector2(-10_000, -10_000)));
        Assert.That(
            option.GetComponent<Position>().Value,
            Is.EqualTo(new Vector2(800, 299)));

        button.RemoveTag<Pressed>();
        root.Update(new UpdateTick(1f / 60f, 0));

        option.AddTag<Pressed>();
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(button.GetComponent<EditorDropdown>().Selected, Is.EqualTo(2));
        Assert.That(button.GetComponent<EditorDropdown>().Open, Is.False);
        Assert.That(commands.TryDequeue(out EditorCommand command), Is.True);
        Assert.That(command.Type, Is.EqualTo(EditorCommandType.SetSnap));
        Assert.That(command.IntValue, Is.EqualTo(16));
    }

    [Test]
    public void SelectingANoteTypeKeepsItForNewNotes()
    {
        var store = new EntityStore();
        var commands = new EditorCommandBus();
        Entity editor = store.CreateEntity(
            new Playhead(0, 120, 0, false),
            new EditorNoteTarget(KeybindAction.M));

        Entity list = store.CreateEntity(new Position(Vector2.Zero));
        Entity button = store.CreateEntity(
            new Position(new Vector2(800, 200)),
            new Rectangle(new Rect2(-95, -17, 190, 34)),
            new Text(""),
            new EditorDropdown(list, EditorDropdownKind.NoteType, 3));
        Entity option = store.CreateEntity(
            new EditorDropdownOption(button, (int)KeybindAction.R3));

        var root = new SystemRoot(store);
        root.Add(new EditorDropdownSystem(store, commands));

        option.AddTag<Pressed>();
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(commands.TryDequeue(out EditorCommand command), Is.True);
        Assert.That(command.Type, Is.EqualTo(EditorCommandType.SetTarget));
        Assert.That(command.IntValue, Is.EqualTo((int)KeybindAction.R3));
    }
}

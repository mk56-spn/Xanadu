using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Editor.Commands;
using Xanadu.Rendering;
using Xanadu.Settings;
using Xanadu.Systems.Editor;
using Xanadu.Systems.Ui;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class EditorDropdownSystemTests
{
    [Test]
    public void TheBlendDropdownSetsTheSelectionAndUndoesAsOneStep()
    {
        var store = new EntityStore();
        var history = new EditorHistory(store);
        Entity editor = store.CreateEntity(
            new Playhead(0, 120, 0, false),
            new NoteSelection(default));
        Entity note = EditorItems.Create(store, new Note(0, Vector2.Zero));
        editor.GetComponent<NoteSelection>().SetMany(new[] { note });

        Entity list = store.CreateEntity(
            new Position(Vector2.Zero),
            new Rectangle(new Rect2(-95, -70, 190, 140)));

        Entity button = store.CreateEntity(
            new Position(new Vector2(800, 200)),
            new Rectangle(new Rect2(-95, -17, 190, 34)),
            new Text(""),
            new Dropdown(list, 0),
            new EditorDropdownControl(EditorDropdownKind.Blend));

        Entity option = store.CreateEntity(
            new Position(Vector2.Zero),
            new Rectangle(new Rect2(-92, -14, 184, 28)),
            new DropdownOption(button, (int)BlendMode.Add));

        var root = new SystemRoot(store);
        root.Add(new EditorDropdownSystem(store, new EditorCommandBus(), history));
        root.Add(new UiDropdownSystem(store));

        button.AddTag<Pressed>();
        root.Update(new UpdateTick(1f / 60f, 0));
        Assert.That(button.GetComponent<Text>().Value, Is.EqualTo("BLEND  MIX  ▾"));

        button.RemoveTag<Pressed>();
        root.Update(new UpdateTick(1f / 60f, 0));

        option.AddTag<Pressed>();
        root.Update(new UpdateTick(1f / 60f, 0));
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(ItemBlend.Of(note), Is.EqualTo(BlendMode.Add));
        Assert.That(button.GetComponent<Text>().Value, Is.EqualTo("BLEND  ADD  ▾"));
        Assert.That(history.CanUndo, Is.True);

        history.Undo();
        Assert.That(
            ItemBlend.Of(store.Query<Note>().ToEntityList()[0]),
            Is.EqualTo(BlendMode.Mix));
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
    public void PressingASwatchSetsTheNoteKey()
    {
        var store = new EntityStore();
        var commands = new EditorCommandBus();
        store.CreateEntity(
            new EditorPlacement { Mode = EditorPlacementMode.Notes, Note = KeybindAction.Action2 });
        Entity swatch = store.CreateEntity(
            new Position(Vector2.Zero),
            EditorButton.NoteType(KeybindAction.Action3));

        var root = new SystemRoot(store);
        root.Add(new EditorNoteTypeBarSystem(store, commands));

        swatch.AddTag<Pressed>();
        root.Update(new UpdateTick(1f / 60f, 0));
        Assert.That(commands.TryDequeue(out EditorCommand command), Is.True);
        Assert.That(command.Type, Is.EqualTo(EditorCommandType.SetTarget));
        Assert.That(command.IntValue, Is.EqualTo((int)KeybindAction.Action3));
    }

    [TestCase(EditorPlacementMode.Notes, true)]
    [TestCase(EditorPlacementMode.Neutral, false)]
    [TestCase(EditorPlacementMode.Dash, false)]
    [TestCase(EditorPlacementMode.Momentum, false)]
    [TestCase(EditorPlacementMode.Mines, false)]
    [TestCase(EditorPlacementMode.Start, false)]
    public void TheNoteKeyBarShowsOnlyForColouredNotes(EditorPlacementMode mode, bool shown)
    {
        var store = new EntityStore();
        store.CreateEntity(
            new EditorPlacement { Mode = mode, Note = KeybindAction.Action2 });
        Entity panel = store.CreateEntity(
            new Position(new Vector2(10, 20)),
            new EditorNoteBarPanel());

        var root = new SystemRoot(store);
        root.Add(new EditorNoteTypeBarSystem(store, new EditorCommandBus()));
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(EditorNoteTypeBarSystem.ShowsFor(mode), Is.EqualTo(shown));
        Assert.That(UiLayout.IsHidden(panel), Is.EqualTo(!shown));
        Assert.That(
            UiLayout.IsHidden(panel),
            Is.EqualTo(!shown));
    }
}

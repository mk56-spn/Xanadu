using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Editor.Commands;
using Xanadu.Systems.Editor;
using Xanadu.Tests.Support;

namespace Xanadu.Tests.Shaders;

[TestFixture]
public sealed class EditorShaderCommandTests : EcsSystemTest
{
    private const string Source = "shader_type canvas_item;\nvoid fragment() { COLOR = vec4(1.0); }";

    private EditorCommandBus _commands = null!;
    private Entity _editor;

    protected override void OnEcsSetUp()
    {
        _commands = new EditorCommandBus();
        Root.Add(new EditorCommandSystem(Store, _commands, new EditorHistory(Store)));
        _editor = Store.CreateEntity(
            new LevelInfo("Untitled", ""),
            new LevelFile(""),
            new Playfield(1000, 1000),
            new Playhead(0, 120, 0, false),
            new EditorMetronome(false),
            new NoteSelection(default),
            LevelGroups.Create(),
            LevelShaders.Create());
    }

    [Test]
    public void ImportPutsTheSourceInTheSlot()
    {
        _commands.Enqueue(EditorCommand.ImportShader(3, "Glow", Source));
        Frame();

        ShaderSlot? slot = _editor.GetComponent<LevelShaders>().Get(3);
        Assert.That(slot, Is.Not.Null);
        Assert.That(slot!.Name, Is.EqualTo("Glow"));
        Assert.That(slot.Source, Is.EqualTo(Source));
    }

    [Test]
    public void ImportRefusesShadersALevelMayNotCarry()
    {
        _commands.Enqueue(EditorCommand.ImportShader(1, "Spatial", "shader_type spatial;"));
        _commands.Enqueue(EditorCommand.ImportShader(11, "Out of range", Source));
        _commands.Enqueue(EditorCommand.ImportShader(0, "Slot zero", Source));
        Frame();

        Assert.That(_editor.GetComponent<LevelShaders>().Slots, Is.All.Null);
    }

    [Test]
    public void AssigningShadersToTheSelectionShadesNotesMinesAndBlocks()
    {
        Entity note = Store.CreateEntity(new Note(500, new Vector2(10, 20)));
        Entity mine = Store.CreateEntity(new Mine(900, new Vector2(30, 40)));
        Entity block = Store.CreateEntity(new Block(new Vector2(50, 60), new Vector2(20, 20)));
        Entity untouched = Store.CreateEntity(new Note(700, new Vector2(1, 1)));
        _editor.GetComponent<NoteSelection>().SetMany([note, mine, block]);

        _commands.Enqueue(EditorCommand.AssignShader(4));
        Frame();

        Assert.That(ItemShader.Of(note), Is.EqualTo(4));
        Assert.That(ItemShader.Of(mine), Is.EqualTo(4));
        Assert.That(ItemShader.Of(block), Is.EqualTo(4));
        Assert.That(ItemShader.Of(untouched), Is.EqualTo(0));
    }

    [Test]
    public void SlotZeroTakesTheShaderOff()
    {
        Entity note = Store.CreateEntity(new Note(500, new Vector2(10, 20)), new ItemShader(2));
        _editor.GetComponent<NoteSelection>().Note = note;

        _commands.Enqueue(EditorCommand.AssignShader(0));
        Frame();

        Assert.That(note.HasComponent<ItemShader>(), Is.False);
    }

    [Test]
    public void AssigningIsUndoable()
    {
        Entity note = Store.CreateEntity(new Note(500, new Vector2(10, 20)));
        _editor.GetComponent<NoteSelection>().Note = note;

        _commands.Enqueue(EditorCommand.AssignShader(2));
        Frame();
        _commands.Enqueue(EditorCommand.Undo());
        Frame();

        Entity restored = Store.Query<Note>().ToEntityList().Single();
        Assert.That(ItemShader.Of(restored), Is.EqualTo(0));

        _commands.Enqueue(EditorCommand.Redo());
        Frame();

        Assert.That(ItemShader.Of(Store.Query<Note>().ToEntityList().Single()), Is.EqualTo(2));
    }

    [Test]
    public void UndoingADeleteBringsTheNoteBackWithItsShader()
    {
        Entity note = Store.CreateEntity(new Note(500, new Vector2(10, 20)), new ItemShader(5));
        _editor.GetComponent<NoteSelection>().Note = note;

        _commands.Enqueue(EditorCommand.DeleteSelection());
        Frame();
        Assert.That(Store.Query<Note>().Count, Is.EqualTo(0));

        _commands.Enqueue(EditorCommand.Undo());
        Frame();

        Assert.That(ItemShader.Of(Store.Query<Note>().ToEntityList().Single()), Is.EqualTo(5));
    }

    [Test]
    public void PastedNotesKeepTheirShader()
    {
        Entity note = Store.CreateEntity(new Note(500, new Vector2(10, 20)), new ItemShader(6));
        _editor.GetComponent<NoteSelection>().Note = note;
        _editor.GetComponent<Playhead>().TimeMs = 2000;

        _commands.Enqueue(EditorCommand.CopySelection());
        _commands.Enqueue(EditorCommand.PasteSelection());
        Frame();
        Frame();

        var notes = Store.Query<Note>().ToEntityList();
        Assert.That(notes, Has.Count.EqualTo(2));
        Assert.That(notes.Select(ItemShader.Of), Is.All.EqualTo(6));
    }

    [Test]
    public void RemovingAShaderClearsItsUsesButLeavesOthers()
    {
        _commands.Enqueue(EditorCommand.ImportShader(1, "A", Source));
        _commands.Enqueue(EditorCommand.ImportShader(2, "B", Source));
        Frame();
        Entity onOne = Store.CreateEntity(new Note(100, new Vector2(1, 1)), new ItemShader(1));
        Entity onTwo = Store.CreateEntity(new Note(200, new Vector2(2, 2)), new ItemShader(2));
        _commands.Enqueue(EditorCommand.SetPlayerShader(1));
        _commands.Enqueue(EditorCommand.SetFieldShader(2));
        Frame();

        _commands.Enqueue(EditorCommand.RemoveShader(1));
        Frame();

        LevelShaders shaders = _editor.GetComponent<LevelShaders>();
        Assert.That(shaders.Get(1), Is.Null);
        Assert.That(shaders.Get(2), Is.Not.Null);
        Assert.That(onOne.HasComponent<ItemShader>(), Is.False);
        Assert.That(ItemShader.Of(onTwo), Is.EqualTo(2));
        Assert.That(shaders.PlayerSlot, Is.EqualTo(0));
        Assert.That(shaders.FieldSlot, Is.EqualTo(2));
    }

    [Test]
    public void PlayerAndFieldShadersAreSetAndClearedBySlot()
    {
        _commands.Enqueue(EditorCommand.SetPlayerShader(3));
        _commands.Enqueue(EditorCommand.SetFieldShader(4));
        _commands.Enqueue(EditorCommand.SetFieldShader(99));
        Frame();

        Assert.That(_editor.GetComponent<LevelShaders>().PlayerSlot, Is.EqualTo(3));
        Assert.That(_editor.GetComponent<LevelShaders>().FieldSlot, Is.EqualTo(4));

        _commands.Enqueue(EditorCommand.SetPlayerShader(0));
        Frame();

        Assert.That(_editor.GetComponent<LevelShaders>().PlayerSlot, Is.EqualTo(0));
    }
}

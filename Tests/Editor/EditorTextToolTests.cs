using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Systems.Editor;
using Xanadu.Systems.Input;
using Xanadu.Tests.Support;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tests.Editor;

/// <summary>The text tool places a label, and the sidebar retypes and resizes it.</summary>
[TestFixture]
public sealed class EditorTextToolTests
{
    private EntityStore _store = null!;
    private FakePointerInput _pointer = null!;
    private EditorHistory _history = null!;
    private SystemRoot _root = null!;
    private Entity _editor;
    private Entity _section;
    private Entity _field;
    private Entity _size;
    private Entity _bigger;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _pointer = new FakePointerInput();
        _history = new EditorHistory(_store);
        _root = new SystemRoot(_store);
        _root.Add(new EditorNoteInputSystem(_store, _pointer, _history));
        _root.Add(new EditorTextPanelSystem(_history));

        _section = _store.CreateEntity();
        _field = _store.CreateEntity(new TextField(""));
        _size = _store.CreateEntity(new Text("SIZE"));
        _bigger = _store.CreateEntity(new EditorButton(EditorButtonKind.TextSize, 1));
        _editor = _store.CreateEntity(
            new LevelInfo("Test", TestSongs.At("/tmp/test.wav")),
            new Playfield(1000, 1000),
            new Playhead(1_000, 120, 0, false),
            new PlayfieldView(Vector2.Zero, 1f, new Vector2(1000, 1000)),
            new NoteSelection(default),
            new EditorPlacement { Mode = EditorPlacementMode.Text },
            new EditorTextPanel(_section, _field, _size));
    }

    private void Frame() => _root.Update(new UpdateTick(1f / 60f, 0));

    private void Click(Vector2 point)
    {
        _pointer.Current = new PointerInput(point, true);
        Frame();
        _pointer.Current = new PointerInput(point, false);
        Frame();
    }

    private Entity OnlyText => _store.Query<LevelText>().ToEntityList().Single();

    [Test]
    public void ClickingTheFieldPlacesSelectedTextThere()
    {
        Click(new Vector2(300, 400));

        Entity text = OnlyText;
        Assert.That(text.GetComponent<LevelText>().Position, Is.EqualTo(new Vector2(300, 400)));
        Assert.That(text.GetComponent<LevelText>().Text, Is.EqualTo(LevelText.Placeholder));
        Assert.That(_editor.GetComponent<NoteSelection>().Note, Is.EqualTo(text));
        Assert.That(_store.Query<Note>().Count, Is.Zero);
        Assert.That(UiLayout.IsHidden(_section), Is.False);
    }

    [Test]
    public void TypingRetypesTheTextLiveAndUndoesInOneStep()
    {
        Click(new Vector2(300, 400));

        _field.AddTag<TextFieldFocused>();
        Frame();
        _field.GetComponent<TextField>().Value = "Hel";
        Frame();
        Assert.That(OnlyText.GetComponent<LevelText>().Text, Is.EqualTo("Hel"));
        _field.GetComponent<TextField>().Value = "Hello";
        Frame();
        _field.RemoveTag<TextFieldFocused>();
        Frame();

        Assert.That(OnlyText.GetComponent<LevelText>().Text, Is.EqualTo("Hello"));
        Assert.That(_history.Undo(), Is.True);
        Assert.That(OnlyText.GetComponent<LevelText>().Text, Is.EqualTo(LevelText.Placeholder));
    }

    [Test]
    public void TheSizeButtonsStepTheSelectedText()
    {
        Click(new Vector2(300, 400));

        _bigger.AddTag<Pressed>();
        Frame();
        _bigger.RemoveTag<Pressed>();
        Frame();

        Assert.That(
            OnlyText.GetComponent<LevelText>().Size,
            Is.EqualTo(LevelText.DefaultSize + EditorTextPanelSystem.SizeStep));
        Assert.That(_size.GetComponent<Text>().Value, Is.EqualTo($"SIZE {LevelText.DefaultSize + EditorTextPanelSystem.SizeStep:0}"));
    }

    [Test]
    public void ThePanelHidesWithNoTextSelected()
    {
        Frame();
        Assert.That(UiLayout.IsHidden(_section), Is.True);
    }

    [Test]
    public void ClickingExistingTextSelectsItInsteadOfPlacingMore()
    {
        Click(new Vector2(300, 400));
        _editor.GetComponent<NoteSelection>().Clear();

        Click(new Vector2(305, 402));

        Assert.That(_store.Query<LevelText>().Count, Is.EqualTo(1));
        Assert.That(_editor.GetComponent<NoteSelection>().Note, Is.EqualTo(OnlyText));
    }
}

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Systems.Editor;
using Xanadu.Systems.Input;
using Xanadu.Systems.Ui;
using Xanadu.Tests.Support;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.Editor;

/// <summary>
/// Regression: letting go of a colour picker drag with nothing selected put the
/// colour back to white. The pick now sticks, as the colour new items get.
/// </summary>
[TestFixture]
public sealed class EditorColourBrushTests
{
    private EntityStore _store = null!;
    private SystemRoot _root = null!;
    private FakePointerInput _pointer = null!;
    private Entity _editor;
    private Entity _field;
    private Entity _square;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _pointer = new FakePointerInput();
        UiBuilder ui = UiBuilder.Begin(_store, new FakeVisualRidFactory()).Layer(0);
        ui.Container(new Vector2(400, 400), new Vector2(300, 400), StackDirection.Vertical);
        _field = UiNode.Wrap(ui.Current, ui.Visuals).ColorField("COLOUR", 252, "tip").Entity;
        _square = _field.GetComponent<UiColorField>().Picker;
        _editor = _store.CreateEntity(
            new NoteSelection(default),
            new EditorPlacement(),
            new EditorSelfModulateField(_field));

        // The real order: the whole UI interaction step, then the editor's colour system.
        _root = new SystemRoot(_store);
        _root.Add(new UiColorDragSystem(_store, _pointer, new FakeVisualRidFactory()));
        _root.Add(new UiColorPickerSystem(_pointer));
        _root.Add(new UiColorFieldSystem());
        _root.Add(new EditorSelfModulateSystem(new EditorHistory(_store)));
        Frame(Vector2.Zero, pressed: false);
    }

    private Vector2 Square => UiTransform.WorldPosition(_square, _square.GetComponent<Position>().Value);

    private void Frame(Vector2 at, bool pressed)
    {
        _pointer.Current = new PointerInput(at, pressed);
        _root.Update(new UpdateTick(1f / 60f, 0));
    }

    /// <summary>Presses the square's centre, drags up and right across it, and lets go; returns the colour held at the end.</summary>
    private Color DragAndRelease()
    {
        Frame(Square, pressed: true);
        for (int i = 1; i <= 10; i++)
            Frame(Square + new Vector2(2 * i, -i), pressed: true);
        Color picked = _square.GetComponent<UiColorPicker>().Color;
        Frame(Square + new Vector2(20, -10), pressed: false);
        for (int i = 0; i < 5; i++)
            Frame(Square + new Vector2(20, -10), pressed: false);
        return picked;
    }

    [Test]
    public void APickWithNothingSelectedStaysAfterLettingGo()
    {
        Color picked = DragAndRelease();

        Assert.That(picked, Is.Not.EqualTo(Colors.White));
        Assert.That(_square.GetComponent<UiColorPicker>().Color, Is.EqualTo(picked), "the picker");
        Assert.That(_field.GetComponent<UiColorField>().Value, Is.EqualTo(picked), "the field");
        Assert.That(_field.GetComponent<TextField>().Value, Is.EqualTo(SelfModulate.Format(picked)), "the hex");
        Assert.That(_editor.GetComponent<EditorPlacement>().Modulate, Is.EqualTo(picked), "the brush");
    }

    [Test]
    public void TheNextPlacedItemWearsThePick()
    {
        Color picked = DragAndRelease();

        Entity block = EditorItems.Place(_store, new Block(Vector2.Zero, new Vector2(40, 40)));
        Entity start = EditorItems.Place(_store, new StartPoint("", 0, Vector2.Zero, System.Numerics.Vector2.Zero, new Vector2(0, -1)));

        Assert.That(SelfModulate.Of(block), Is.EqualTo(picked));
        Assert.That(start.HasComponent<SelfModulate>() && SelfModulate.Of(start) != Colors.White, Is.False,
            "only tintable items take it");
    }

    [Test]
    public void APickWithAnItemSelectedColoursItAndLeavesTheBrush()
    {
        Entity note = EditorItems.Create(_store, new Note(0, Vector2.Zero));
        _editor.GetComponent<NoteSelection>().Note = note;
        Frame(Vector2.Zero, pressed: false);

        Color picked = DragAndRelease();

        Assert.That(SelfModulate.Of(note), Is.EqualTo(picked));
        Assert.That(_field.GetComponent<UiColorField>().Value, Is.EqualTo(picked));
        Assert.That(_editor.GetComponent<EditorPlacement>().Modulate, Is.EqualTo(Colors.White));
    }
}

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Xanadu.Core.Maths;
using Xanadu.Ecs;
using Xanadu.Model.Ecs.Components;
using Xanadu.Editor;
using Xanadu.Systems.Editor;
using Xanadu.Systems.Input;
using Xanadu.Systems.Ui;
using Xanadu.Tests.Support;
using Xanadu.Tests.Support.Fakes;
using Xanadu.Ui.Ecs;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Input;
using Xanadu.Ui.Systems.Ui;
using Xanadu.Model.Editor;
using Position = Xanadu.Core.Position;

namespace Xanadu.Tests.Editor;

/// <summary>The sidebar's colour field tints every selected item, typed or picked.</summary>
[TestFixture]
public sealed class EditorMultiSelectionTintTests
{
    private EntityStore _store = null!;
    private SystemRoot _root = null!;
    private FakePointerInput _pointer = null!;
    private Entity _field;
    private Entity _square;
    private Entity _note;
    private Entity _block;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _pointer = new FakePointerInput();
        UiBuilder ui = UiBuilder.Begin(_store, new FakeVisualRidFactory()).Layer(0);
        ui.Container(new Vector2(400, 400), new Vector2(300, 400), StackDirection.Vertical);
        _field = UiNode.Wrap(ui.Current, ui.Visuals).ColorField("COLOUR", 252, "tip").Entity;
        _square = _field.GetComponent<UiColorField>().Picker;

        _note = EditorItems.Create(_store, new Note(0, Vector2.Zero));
        _block = EditorItems.Create(_store, new Block(new Vector2(40, 40), new Vector2(40, 40)));
        var selection = new NoteSelection(_note);
        selection.SetMany(new[] { _note, _block });
        _store.CreateEntity(selection, new EditorSelfModulateField(_field));

        _root = new SystemRoot(_store);
        _root.Add(new UiColorPickerSystem(_pointer));
        _root.Add(new UiColorFieldSystem());
        _root.Add(new EditorSelfModulateSystem(new EditorHistory(_store)));
        Frame(Vector2.Zero, false);
    }

    private Vector2 Square => UiTransform.WorldPosition(_square, _square.GetComponent<Position>().Value);

    private void Frame(Vector2 at, bool pressed)
    {
        _pointer.Current = new PointerInput(at, pressed);
        _root.Update(new UpdateTick(1f / 60f, 0));
    }

    /// <summary>
    /// Regression: a colour dragged onto the field used to show for a frame and
    /// then snap back, because nothing had picked the items it was for.
    /// </summary>
    [Test]
    public void ADroppedColourTintsTheSelectionAndStays()
    {
        ref UiColorDrop drop = ref _field.GetComponent<UiColorDrop>();
        drop.Pending = true;
        drop.Color = Colors.Red;

        for (int i = 0; i < 5; i++)
            Frame(Vector2.Zero, false);

        Assert.That(SelfModulate.Of(_note), Is.EqualTo(Colors.Red));
        Assert.That(SelfModulate.Of(_block), Is.EqualTo(Colors.Red));
        Assert.That(_field.GetComponent<UiColorField>().Value, Is.EqualTo(Colors.Red));
        Assert.That(_field.GetComponent<TextField>().Value, Is.EqualTo(SelfModulate.Format(Colors.Red)));
        Assert.That(UiColorField.IsEditing(_field), Is.False, "the drop's edit ends once it is applied");
    }

    [Test]
    public void ADroppedColourOnTheSwatchStaysToo()
    {
        Entity swatch = _field.GetComponent<UiColorField>().Swatch;
        ref UiColorDrop drop = ref swatch.GetComponent<UiColorDrop>();
        drop.Pending = true;
        drop.Color = Colors.Lime;

        for (int i = 0; i < 5; i++)
            Frame(Vector2.Zero, false);

        Assert.That(SelfModulate.Of(_note), Is.EqualTo(Colors.Lime));
        Assert.That(_field.GetComponent<UiColorField>().Value, Is.EqualTo(Colors.Lime));
    }

    [Test]
    public void ATypedColourTintsTheWholeSelection()
    {
        _field.AddTag<TextFieldFocused>();
        Frame(Vector2.Zero, false);
        _field.GetComponent<TextField>().Value = "#ff0000";
        Frame(Vector2.Zero, false);
        _field.RemoveTag<TextFieldFocused>();
        Frame(Vector2.Zero, false);
        Frame(Vector2.Zero, false);

        Assert.That(SelfModulate.Of(_note), Is.EqualTo(Colors.Red));
        Assert.That(SelfModulate.Of(_block), Is.EqualTo(Colors.Red));
    }

    [Test]
    public void APickedColourTintsTheWholeSelection()
    {
        Frame(Square, true);
        Frame(Square + new Vector2(30, 0), true);
        Frame(Square + new Vector2(30, 0), false);
        Frame(Square + new Vector2(30, 0), false);

        Color picked = _square.GetComponent<UiColorPicker>().Color;
        Assert.That(picked, Is.Not.EqualTo(Colors.White));
        Assert.That(SelfModulate.Of(_note), Is.EqualTo(picked));
        Assert.That(SelfModulate.Of(_block), Is.EqualTo(picked));
    }
}

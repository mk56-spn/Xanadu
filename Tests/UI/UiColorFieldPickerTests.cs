using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Input;
using Xanadu.Systems.Ui;
using Xanadu.Tests.Support;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.UI;

[TestFixture]
public sealed class UiColorFieldPickerTests
{
    private EntityStore _store = null!;
    private SystemRoot _root = null!;
    private FakePointerInput _pointer = null!;
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
        _root = new SystemRoot(_store);
        _root.Add(new UiColorPickerSystem(_pointer));
        _root.Add(new UiColorFieldSystem());
    }

    private Vector2 Square => UiTransform.WorldPosition(_square, _square.GetComponent<Position>().Value);

    private void Frame(Vector2 at, bool pressed)
    {
        _pointer.Current = new PointerInput(at, pressed);
        _root.Update(new UpdateTick(1f / 60f, 0));
    }

    [Test]
    public void AFieldHasAPickerWithoutItsOwnSwatchOrHex()
    {
        UiColorPicker picker = _square.GetComponent<UiColorPicker>();
        Assert.That(picker.Swatch.Tags.Has<UiHidden>(), Is.True);
        Assert.That(picker.Hex.Tags.Has<UiHidden>(), Is.True);
        Assert.That(picker.Alpha.Tags.Has<UiHidden>(), Is.False);
    }

    [Test]
    public void DraggingShowsLiveAndCommitsOnceWhenLetGo()
    {
        Frame(Square, pressed: true);
        Frame(Square + new Vector2(30, 0), pressed: true);

        Assert.That(UiColorField.TakeCommit(_field, out _), Is.False);
        Color shown = _field.GetComponent<UiColorField>().Swatch.GetComponent<FillColor>().Value;
        Assert.That(shown, Is.Not.EqualTo(Colors.White));
        Assert.That(_field.GetComponent<TextField>().Value, Is.EqualTo(SelfModulate.Format(shown)));
        Assert.That(UiColorField.IsEditing(_field), Is.True);

        Frame(Square + new Vector2(30, 0), pressed: false);

        Assert.That(UiColorField.IsEditing(_field), Is.False);
        Assert.That(UiColorField.TakeCommit(_field, out Color color), Is.True);
        Assert.That(color, Is.EqualTo(shown));
        Assert.That(UiColorField.TakeCommit(_field, out _), Is.False);
    }

    [Test]
    public void AClickThatChangesNothingCommitsNothing()
    {
        // Top right of the square at hue 0 is pure red, which is what the field holds.
        UiColorField.Show(_field, Colors.Red);
        Frame(new Vector2(-1000, -1000), pressed: false);
        Vector2 corner = Square + new Vector2(60, -60);

        Frame(corner, pressed: true);
        Frame(corner, pressed: false);

        Assert.That(UiColorField.TakeCommit(_field, out _), Is.False);
    }

    [Test]
    public void ThePickerFollowsTheColourItIsShown()
    {
        UiColorField.Show(_field, new Color("#00ff00"));
        Frame(new Vector2(-1000, -1000), pressed: false);

        Assert.That(_square.GetComponent<UiColorPicker>().Color.IsEqualApprox(new Color("#00ff00")), Is.True);
    }

    [Test]
    public void APickerClippedOutOfItsPanelTakesNoPress()
    {
        Entity panel = _square.Parent.Parent;
        panel.GetComponent<UiContainer>().Clip = true;
        panel.GetComponent<Rectangle>().Bounds = new Rect2(0, 5000, 10, 10);

        Frame(Square, pressed: true);

        Assert.That(_square.GetComponent<UiColorPicker>().Dragging, Is.False);
    }
}

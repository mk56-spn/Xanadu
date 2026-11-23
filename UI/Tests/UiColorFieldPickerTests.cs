using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Xanadu.Tests.Support.Fakes;
using Xanadu.Ui.Ecs;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Input;
using Xanadu.Ui.Systems.Layout;
using Xanadu.Ui.Systems.Ui;
using Position = Xanadu.Core.Position;

namespace Xanadu.Ui.Tests;

[TestFixture]
public sealed class UiColorFieldPickerTests
{
    private EntityStore _store = null!;
    private SystemRoot _root = null!;
    private FakePointerInput _pointer = null!;
    private Entity _field;
    private Entity _picker;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _pointer = new FakePointerInput();
        UiBuilder ui = UiBuilder.Begin(_store, new FakeVisualRidFactory()).Layer(0);
        ui.Container(new Vector2(400, 400), new Vector2(300, 400), StackDirection.Vertical);
        _field = UiNode.Wrap(ui.Current, ui.Visuals).ColorField("COLOUR", 252, "tip").Entity;
        _picker = _field.GetComponent<UiColorField>().Picker;
        _root = new SystemRoot(_store);
        _root.Add(new ContainerLayoutSystem());
        _root.Add(new UiColorPickerSystem(_pointer));
        _root.Add(new UiColorFieldSystem());

        // Lay the picker out before any test points at its square.
        Frame(new Vector2(-1000, -1000), false);
    }

    /// <summary>The centre of the picker's square, as laid out.</summary>
    private Vector2 Square
    {
        get
        {
            Entity square = _picker.GetComponent<UiColorPicker>().Square;
            return UiTransform.WorldPosition(square, square.GetComponent<Position>().Value);
        }
    }

    private void Frame(Vector2 at, bool pressed)
    {
        _pointer.Current = new PointerInput(at, pressed);
        _root.Update(new UpdateTick(1f / 60f, 0));
    }

    [Test]
    public void AFieldHasAPickerWithoutItsOwnSwatchOrHex()
    {
        UiColorPicker picker = _picker.GetComponent<UiColorPicker>();
        Assert.That(picker.Swatch.IsNull, Is.True);
        Assert.That(picker.Hex.IsNull, Is.True);
        Assert.That(picker.Alpha.IsNull, Is.False);
    }

    [Test]
    public void DraggingShowsLiveAndCommitsOnceWhenLetGo()
    {
        Frame(Square, true);
        Frame(Square + new Vector2(30, 0), true);

        Assert.That(UiColorField.TakeCommit(_field, out _), Is.False);
        Color shown = _field.GetComponent<UiColorField>().Swatch.GetComponent<FillColor>().Value;
        Assert.That(shown, Is.Not.EqualTo(Colors.White));
        Assert.That(_field.GetComponent<TextField>().Value, Is.EqualTo(SelfModulate.Format(shown)));
        Assert.That(UiColorField.IsEditing(_field), Is.True);

        Frame(Square + new Vector2(30, 0), false);

        Assert.That(UiColorField.IsEditing(_field), Is.False);
        Assert.That(UiColorField.TakeCommit(_field, out Color color), Is.True);
        Assert.That(color, Is.EqualTo(shown));
        Assert.That(UiColorField.TakeCommit(_field, out _), Is.False);
    }

    [Test]
    public void DraggingHandsOutTheColourLiveAndStillCommitsWhenLetGo()
    {
        Frame(Square, true);
        Assert.That(UiColorField.TakeLive(_field, out Color first), Is.True);
        Assert.That(UiColorField.TakeLive(_field, out _), Is.False, "only when it changes");

        Frame(Square + new Vector2(30, 0), true);
        Assert.That(UiColorField.TakeLive(_field, out Color moved), Is.True);
        Assert.That(moved, Is.Not.EqualTo(first));

        // The tool shows the live colour on its targets, and reports it back while dragging.
        UiColorField.Show(_field, moved);
        Frame(Square + new Vector2(30, 0), false);

        Assert.That(UiColorField.TakeLive(_field, out _), Is.False);
        Assert.That(UiColorField.TakeCommit(_field, out Color committed), Is.True, "the drag still commits");
        Assert.That(committed, Is.EqualTo(moved));
    }

    [Test]
    public void AClickThatChangesNothingCommitsNothing()
    {
        // Top right of the square at hue 0 is pure red, which is what the field holds.
        UiColorField.Show(_field, Colors.Red);
        Frame(new Vector2(-1000, -1000), false);
        Vector2 corner = Square + new Vector2(60, -60);

        Frame(corner, true);
        Frame(corner, false);

        Assert.That(UiColorField.TakeCommit(_field, out _), Is.False);
    }

    [Test]
    public void ThePickerFollowsTheColourItIsShown()
    {
        UiColorField.Show(_field, new Color("#00ff00"));
        Frame(new Vector2(-1000, -1000), false);

        Assert.That(_picker.GetComponent<UiColorPicker>().Color.IsEqualApprox(new Color("#00ff00")), Is.True);
    }

    [Test]
    public void APickerClippedOutOfItsPanelTakesNoPress()
    {
        Entity panel = _picker.Parent.Parent;
        panel.GetComponent<UiContainer>().Clip = true;
        panel.GetComponent<Rectangle>().Bounds = new Rect2(0, 5000, 10, 10);

        Frame(Square, true);

        Assert.That(_picker.GetComponent<UiColorPicker>().Dragging, Is.False);
    }
}

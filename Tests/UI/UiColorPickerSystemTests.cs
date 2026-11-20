using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Xanadu.Core.Maths;
using Position = Xanadu.Core.Position;
using Rect2 = Xanadu.Core.Maths.Rect2;
using Xanadu.Tests.Support;
using Xanadu.Tests.Support.Fakes;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Ecs;
using Xanadu.Ui.Systems.Input;
using Xanadu.Ui.Systems.Layout;
using Xanadu.Ui.Systems.Ui;

namespace Xanadu.Tests.UI;

[TestFixture]
public sealed class UiColorPickerSystemTests
{
    private static readonly Vector2 First = new(200, 200);
    private static readonly Vector2 Second = new(200, 500);

    private EntityStore _store = null!;
    private SystemRoot _root = null!;
    private FakePointerInput _pointer = null!;
    private Entity _a;
    private Entity _b;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _pointer = new FakePointerInput();
        UiBuilder ui = UiBuilder.Begin(_store, new FakeVisualRidFactory()).Layer(0);
        UiNode layer = UiNode.Wrap(ui.Current, ui.Visuals);
        _a = layer.ColorPicker(new Color("#ff0000")).At(First).Entity;
        _b = layer.ColorPicker(new Color("#00ff00")).At(Second).Entity;
        _root = new SystemRoot(_store);
        _root.Add(new ContainerLayoutSystem());
        _root.Add(new UiColorPickerSystem(_pointer));

        // Lay the pickers out before any test points at their parts.
        Frame(new Vector2(-1000, -1000), false);
    }

    private Rect2 SquareOf(Entity picker) => Box(picker.GetComponent<UiColorPicker>().Square);

    /// <summary>Where a part sits on screen, as laid out.</summary>
    private static Rect2 Box(Entity part)
    {
        Rect2 bounds = part.GetComponent<Rectangle>().Bounds;
        Vector2 world = UiTransform.WorldPosition(part, part.GetComponent<Position>().Value);
        return new Rect2(world + bounds.Position, bounds.Size);
    }

    [Test]
    public void SquareSetsSaturationAndValueFromThePointer()
    {
        // A quarter across and a quarter down: 25% saturation, 75% value.
        Frame(SquareOf(_a).GetCenter() - ColorPickerLayout.Size / 4, true);

        UiColorPicker picker = _a.GetComponent<UiColorPicker>();
        Assert.That(picker.Saturation, Is.EqualTo(0.25f).Within(0.01f));
        Assert.That(picker.Value, Is.EqualTo(0.75f).Within(0.01f));
    }

    [Test]
    public void HueBarSetsTheHueAndKeepsTheSquaresColour()
    {
        Rect2 bar = Box(_a.GetComponent<UiColorPicker>().HueBar);
        Frame(bar.GetCenter(), true);

        UiColorPicker picker = _a.GetComponent<UiColorPicker>();
        Assert.That(picker.Hue, Is.EqualTo(0.5f).Within(0.01f));
        Assert.That(picker.Color.B, Is.GreaterThan(0.9f));
    }

    [Test]
    public void AlphaBarSetsAlpha()
    {
        Rect2 bar = Box(_a.GetComponent<UiColorPicker>().Alpha);
        Frame(bar.Position + new Vector2(bar.Size.X * 0.25f, bar.Size.Y / 2), true);

        Assert.That(_a.GetComponent<UiColorPicker>().AlphaValue, Is.EqualTo(0.25f).Within(0.01f));
    }

    [Test]
    public void DragKeepsFollowingOutsideTheSquareButStaysOnItsPicker()
    {
        Vector2 square = SquareOf(_a).GetCenter();
        Frame(square, true);
        Frame(square + new Vector2(500, 500), true);

        UiColorPicker picker = _a.GetComponent<UiColorPicker>();
        Assert.That(picker.Saturation, Is.EqualTo(1f));
        Assert.That(picker.Value, Is.EqualTo(0f));
        Assert.That(_b.GetComponent<UiColorPicker>().Color, Is.EqualTo(new Color("#00ff00")));
    }

    [Test]
    public void PressingElsewhereAndSlidingOntoAPickerDoesNotGrabIt()
    {
        Frame(new Vector2(900, 900), true);
        Frame(SquareOf(_a).GetCenter(), true);

        Assert.That(_a.GetComponent<UiColorPicker>().Color, Is.EqualTo(new Color("#ff0000")));
    }

    [Test]
    public void HueSurvivesPassingThroughGreyAndBlack()
    {
        ref UiColorPicker picker = ref _a.GetComponent<UiColorPicker>();
        picker.Hue = 0.6f;

        picker.Color = new Color(0.5f, 0.5f, 0.5f);
        Assert.That(picker.Hue, Is.EqualTo(0.6f));

        picker.Color = Colors.Black;
        Assert.That(picker.Hue, Is.EqualTo(0.6f));

        picker.Color = new Color("#0000ff");
        Assert.That(picker.Hue, Is.EqualTo(2f / 3f).Within(0.01f));
    }

    [Test]
    public void SwatchAndHexFollowThePickedColour()
    {
        // Past the top right corner of the square: full saturation and value, still red.
        Vector2 corner = SquareOf(_a).GetCenter() + new Vector2(60, -60);
        Frame(corner, true);
        Frame(corner, false);

        UiColorPicker picker = _a.GetComponent<UiColorPicker>();
        Assert.That(picker.Swatch.GetComponent<FillColor>().Value, Is.EqualTo(picker.Color));
        Assert.That(picker.Hex.GetComponent<Text>().Value, Is.EqualTo("#ff0000"));
    }

    private void Frame(Vector2 position, bool pressed)
    {
        _pointer.Current = new PointerInput(position, pressed);
        _root.Update(new UpdateTick(1f / 60f, 0));
    }
}

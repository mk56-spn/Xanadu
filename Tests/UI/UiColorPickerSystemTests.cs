using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Input;
using Xanadu.Systems.Ui;
using Xanadu.Tests.Support;

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
        _a = ui.ColorPicker(First, new Color("#ff0000"))[0];
        _b = ui.ColorPicker(Second, new Color("#00ff00"))[0];
        _root = new SystemRoot(_store);
        _root.Add(new UiColorPickerSystem(_pointer));
    }

    [Test]
    public void SquareSetsSaturationAndValueFromThePointer()
    {
        // A quarter across and a quarter down: 25% saturation, 75% value.
        Frame(First + new Vector2(-30, -30), pressed: true);

        UiColorPicker picker = _a.GetComponent<UiColorPicker>();
        Assert.That(picker.Saturation, Is.EqualTo(0.25f).Within(0.01f));
        Assert.That(picker.Value, Is.EqualTo(0.75f).Within(0.01f));
    }

    [Test]
    public void HueBarSetsTheHueAndKeepsTheSquaresColour()
    {
        Rect2 bar = ColorPickerLayout.HueBar(First);
        Frame(bar.GetCenter(), pressed: true);

        UiColorPicker picker = _a.GetComponent<UiColorPicker>();
        Assert.That(picker.Hue, Is.EqualTo(0.5f).Within(0.01f));
        Assert.That(picker.Color.B, Is.GreaterThan(0.9f));
    }

    [Test]
    public void AlphaBarSetsAlpha()
    {
        Rect2 bar = ColorPickerLayout.AlphaBar(First);
        Frame(bar.Position + new Vector2(bar.Size.X * 0.25f, bar.Size.Y / 2), pressed: true);

        Assert.That(_a.GetComponent<UiColorPicker>().AlphaValue, Is.EqualTo(0.25f).Within(0.01f));
    }

    [Test]
    public void DragKeepsFollowingOutsideTheSquareButStaysOnItsPicker()
    {
        Frame(First, pressed: true);
        Frame(First + new Vector2(500, 500), pressed: true);

        UiColorPicker picker = _a.GetComponent<UiColorPicker>();
        Assert.That(picker.Saturation, Is.EqualTo(1f));
        Assert.That(picker.Value, Is.EqualTo(0f));
        Assert.That(_b.GetComponent<UiColorPicker>().Color, Is.EqualTo(new Color("#00ff00")));
    }

    [Test]
    public void PressingElsewhereAndSlidingOntoAPickerDoesNotGrabIt()
    {
        Frame(new Vector2(900, 900), pressed: true);
        Frame(First, pressed: true);

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
        Frame(First + new Vector2(60, -60), pressed: true);
        Frame(First + new Vector2(60, -60), pressed: false);

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

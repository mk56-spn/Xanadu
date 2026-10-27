using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Input;
using Xanadu.Tests.Support;
using Xanadu.Systems.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.UI;

[TestFixture]
public sealed class UiSliderSystemTests
{
    private EntityStore _store = null!;
    private SystemRoot _root = null!;
    private FakePointerInput _pointer = null!;
    private Entity _upper;
    private Entity _lower;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _pointer = new FakePointerInput();
        _upper = Slider(new Vector2(100, 100));
        _lower = Slider(new Vector2(100, 140));
        _root = new SystemRoot(_store);
        _root.Add(new UiSliderSystem(_pointer));
    }

    [Test]
    public void DraggingOneSliderLeavesTheOthersAlone()
    {
        Frame(new Vector2(75, 100), pressed: true);
        Frame(new Vector2(100, 100), pressed: true);

        Assert.That(_upper.GetComponent<UiSlider>().Value, Is.EqualTo(50f).Within(0.01f));
        Assert.That(_lower.GetComponent<UiSlider>().Value, Is.EqualTo(0f));
    }

    [Test]
    public void DragKeepsFollowingAfterThePointerLeavesTheSlider()
    {
        Frame(new Vector2(60, 100), pressed: true);
        Frame(new Vector2(130, 180), pressed: true);

        Assert.That(_upper.GetComponent<UiSlider>().Value, Is.EqualTo(80f).Within(0.01f));
        Assert.That(_lower.GetComponent<UiSlider>().Value, Is.EqualTo(0f));
    }

    [Test]
    public void ReleasingEndsTheDrag()
    {
        Frame(new Vector2(60, 100), pressed: true);
        Frame(new Vector2(60, 100), pressed: false);
        Frame(new Vector2(140, 100), pressed: false);

        Assert.That(_upper.GetComponent<UiSlider>().Value, Is.EqualTo(10f).Within(0.01f));
    }

    [Test]
    public void PressingElsewhereAndSlidingOntoASliderDoesNotGrabIt()
    {
        Frame(new Vector2(500, 500), pressed: true);
        Frame(new Vector2(100, 100), pressed: true);

        Assert.That(_upper.GetComponent<UiSlider>().Value, Is.EqualTo(0f));
        Assert.That(_lower.GetComponent<UiSlider>().Value, Is.EqualTo(0f));
    }

    private Entity Slider(Vector2 center) => _store.CreateEntity(
        new UiSlider(default, default, 0, 100, 0),
        new Rectangle(new Rect2(-50, -7, 100, 14)),
        new Position(center));

    private void Frame(Vector2 position, bool pressed)
    {
        _pointer.Current = new PointerInput(position, pressed);
        _root.Update(new UpdateTick(1f / 60f, 0));
    }
}

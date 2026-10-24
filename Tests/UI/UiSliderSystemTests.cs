using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Systems.Input;
using Writer.Systems.Ui;
using Writer.Tests.Support;
using Position = Writer.Ecs.Position;

namespace Writer.Tests.UI;

[TestFixture]
public sealed class UiSliderSystemTests : EcsSystemTest
{
    private ScriptedPointer _pointer = null!;

    protected override void OnEcsSetUp()
    {
        _pointer = new ScriptedPointer();
        Root.Add(new UiSliderSystem(_pointer));
    }

    [Test]
    public void DraggingUpdatesValueFillAndLabel()
    {
        Entity fill = Store.CreateEntity(
            new Rectangle(new Rect2(-50, -8, 100, 16)),
            new Position(new Vector2(150, 100)));
        Entity label = Store.CreateEntity(new Text("SLIDER 50"));
        Entity track = Store.CreateEntity(
            new Rectangle(new Rect2(-100, -8, 200, 16)),
            new Position(new Vector2(200, 100)),
            new UiSlider(fill, label, 0, 100, 50, "SLIDER "));

        _pointer.Current = new PointerInput(new Vector2(250, 100), true);
        Frame();

        Assert.That(track.GetComponent<UiSlider>().Value,
            Is.EqualTo(75f).Within(0.01f));
        Assert.That(label.GetComponent<Text>().Value, Is.EqualTo("SLIDER 75"));
        Assert.That(fill.GetComponent<Rectangle>().Bounds.Size.X,
            Is.EqualTo(150f).Within(0.01f));

        _pointer.Current = new PointerInput(new Vector2(300, 100), true);
        Frame();

        Assert.That(track.GetComponent<UiSlider>().Value,
            Is.EqualTo(100f).Within(0.01f));
        Assert.That(label.GetComponent<Text>().Value, Is.EqualTo("SLIDER 100"));
    }

    private sealed class ScriptedPointer : IPointerInputSource
    {
        public PointerInput Current;

        public PointerInput Read() => Current;
    }
}

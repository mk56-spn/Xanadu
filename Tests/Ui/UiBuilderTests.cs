using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Ui;
using LayerComponent = Writer.Ecs.Layer;
using Position = Writer.Ecs.Position;

namespace Writer.Tests.Ui;

[TestFixture]
public sealed class UiBuilderTests
{
    private EntityStore _store = null!;
    private IVisualRidFactory _visuals = null!;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _visuals = new FakeVisualRidFactory();
    }

    [Test]
    public void LayerAndRectBuildHierarchy()
    {
        UiBuilder builder = UiBuilder.Begin(_store, _visuals).Layer(7);
        Entity layer = builder.Current;

        builder.Rect(
            new Vector2(10, 20),
            new Vector2(30, 40),
            Colors.Red,
            "Hi",
            takesInput: true);

        Entity rect = FirstChild(layer);

        Assert.That(layer.HasComponent<LayerComponent>(), Is.True);
        Assert.That(layer.GetComponent<LayerComponent>().ZIndex, Is.EqualTo(7));
        Assert.That(layer.ChildCount, Is.EqualTo(1));
        Assert.That(rect.Parent, Is.EqualTo(layer));
        Assert.That(rect.GetComponent<Drawable>().ZIndex, Is.EqualTo(7));
        Assert.That(rect.GetComponent<Position>().Value, Is.EqualTo(new Vector2(10, 20)));
        Assert.That(rect.GetComponent<Rectangle>().Bounds, Is.EqualTo(new Rect2(-15, -20, 30, 40)));
        Assert.That(rect.GetComponent<Text>().Value, Is.EqualTo("Hi"));
        Assert.That(rect.Tags.Has<Uninitialised>(), Is.True);
        Assert.That(rect.Tags.Has<Dirty>(), Is.True);
        Assert.That(rect.Tags.Has<TakesInput>(), Is.True);
    }

    [Test]
    public void ContainerNestingAndEndTrackParents()
    {
        UiBuilder builder = UiBuilder.Begin(_store, _visuals).Layer(0);
        Entity layer = builder.Current;

        builder.Container(
            new Vector2(100, 50),
            new Vector2(200, 100),
            StackDirection.Horizontal,
            spacing: 8,
            clip: true);

        Entity outer = builder.Current;
        Assert.That(outer.Parent, Is.EqualTo(layer));

        builder.Rect(Vector2.Zero, new Vector2(20, 20), Colors.Blue)
            .Container(Vector2.Zero, new Vector2(60, 40), StackDirection.Vertical);

        Entity inner = builder.Current;
        Assert.That(inner.Parent, Is.EqualTo(outer));

        builder.Rect(Vector2.Zero, new Vector2(20, 20), Colors.Green);
        Assert.That(inner.ChildCount, Is.EqualTo(1));

        builder.End();
        Assert.That(builder.Current, Is.EqualTo(outer));

        builder.Rect(Vector2.Zero, new Vector2(20, 20), Colors.Yellow);
        Assert.That(outer.ChildCount, Is.EqualTo(3));

        builder.End();
        Assert.That(builder.Current, Is.EqualTo(layer));

        builder.Rect(Vector2.Zero, new Vector2(20, 20), Colors.White);
        Assert.That(layer.ChildCount, Is.EqualTo(2));

        Assert.That(outer.HasComponent<UiContainer>(), Is.True);
        Assert.That(outer.GetComponent<UiContainer>().Direction, Is.EqualTo(StackDirection.Horizontal));
        Assert.That(outer.GetComponent<UiContainer>().Spacing, Is.EqualTo(8));
        Assert.That(outer.GetComponent<UiContainer>().Clip, Is.True);
        Assert.That(outer.Tags.Has<LayoutDirty>(), Is.True);
        Assert.That(inner.Tags.Has<LayoutDirty>(), Is.True);
    }

    [Test]
    public void RectWithoutTextOrInputOmitsThoseComponents()
    {
        UiBuilder builder = UiBuilder.Begin(_store, _visuals).Layer(0);
        Entity layer = builder.Current;

        builder.Rect(Vector2.Zero, new Vector2(20, 20), Colors.White);
        Entity rect = FirstChild(layer);

        Assert.That(rect.HasComponent<Text>(), Is.False);
        Assert.That(rect.Tags.Has<TakesInput>(), Is.False);
    }

    private static Entity FirstChild(Entity parent)
    {
        foreach (Entity child in parent.ChildEntities)
            return child;

        Assert.Fail("Parent has no children.");
        return default;
    }

    private sealed class FakeVisualRidFactory : IVisualRidFactory
    {
        public Rid CreateCanvasItem() => default;
    }
}

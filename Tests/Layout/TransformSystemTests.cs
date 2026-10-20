using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Systems.Canvas;
using Writer.Systems.Layout;
using Position = Writer.Ecs.Position;

namespace Writer.Tests.Layout;

[TestFixture]
public sealed class TransformSystemTests
{
    private EntityStore _store = null!;
    private RecordingCanvas _canvas = null!;
    private SystemRoot _root = null!;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _canvas = new RecordingCanvas();
        _root = new SystemRoot(_store) { new TransformSystem(_canvas) };
    }

    [Test]
    public void MovedEntityUpdatesTransformAndClearsTag()
    {
        Entity entity = _store.CreateEntity(
            default(VisualRid),
            new Position(new Vector2(10, 20)),
            Tags.Get<Moved>());

        Frame();

        Assert.That(_canvas.Transforms, Has.Count.EqualTo(1));
        Assert.That(_canvas.Transforms[0].Origin, Is.EqualTo(new Vector2(10, 20)));
        Assert.That(entity.Tags.Has<Moved>(), Is.False);
    }

    [Test]
    public void CleanEntityDoesNotUpdateTransform()
    {
        _store.CreateEntity(default(VisualRid), new Position(new Vector2(10, 20)));

        Frame();

        Assert.That(_canvas.Transforms, Is.Empty);
    }

    private void Frame() => _root.Update(new UpdateTick(1f / 60f, 0));

    private sealed class RecordingCanvas : IRectCanvas
    {
        public List<Transform2D> Transforms { get; } = new();

        public void SetVisible(Rid canvas, bool visible) { }

        public void SetTransform(Rid canvas, Transform2D transform) => Transforms.Add(transform);

        public void Clear(Rid canvas) { }

        public void DrawRect(Rid canvas, Rect2 bounds, Color color) { }

        public void DrawText(Rid canvas, Rect2 bounds, string text, Color color) { }

        public void DrawTextLeft(Rid canvas, Rect2 bounds, string text, Color color) { }

        public void DrawTextRight(Rid canvas, Rect2 bounds, string text, Color color) { }
    }
}

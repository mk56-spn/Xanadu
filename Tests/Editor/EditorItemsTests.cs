using Position = Xanadu.Ecs.Position;
using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class EditorItemsTests
{
    [Test]
    public void FactoryTagsItemsAndHelpersHandleAllTypes()
    {
        var store = new EntityStore();
        Entity note = EditorItems.Create(store, new Note(
            100, new Vector2(10, 20)));
        Entity mine = EditorItems.Create(store, new Mine(
            200, new Vector2(30, 40)));
        Entity start = EditorItems.Create(store, new StartPoint(
            "Start", 300, new Vector2(50, 60),
            System.Numerics.Vector2.Zero, new Vector2(0, -1)));

        foreach ((Entity entity, long time, Vector2 position) in new[]
                 {
                     (note, 100L, new Vector2(10, 20)),
                     (mine, 200L, new Vector2(30, 40)),
                     (start, 300L, new Vector2(50, 60)),
                 })
        {
            Assert.That(EditorItems.IsItem(entity), Is.True);
            Assert.That(EditorItemTiming.TryGet(entity, out long actualTime),
                Is.True);
            Assert.That(actualTime, Is.EqualTo(time));
            Assert.That(EditorItemPosition.TryGet(entity, out Vector2 actualPosition),
                Is.True);
            Assert.That(actualPosition, Is.EqualTo(position));

            Assert.That(EditorItemTiming.Set(entity, time + 50), Is.True);
            Assert.That(EditorItemPosition.Set(entity, position + Vector2.One),
                Is.True);
            Assert.That(EditorItemTiming.TryGet(entity, out actualTime), Is.True);
            Assert.That(actualTime, Is.EqualTo(time + 50));
            Assert.That(EditorItemPosition.TryGet(entity, out actualPosition),
                Is.True);
            Assert.That(actualPosition, Is.EqualTo(position + Vector2.One));
        }
    }

    [Test]
    public void HelpersRejectNonEditorEntities()
    {
        var store = new EntityStore();
        Entity plain = store.CreateEntity(new Position(Vector2.Zero));

        Assert.That(EditorItems.IsItem(plain), Is.False);
        Assert.That(EditorItemTiming.TryGet(plain, out _), Is.False);
        Assert.That(EditorItemPosition.TryGet(plain, out _), Is.False);
        Assert.That(EditorItemTiming.Set(plain, 1), Is.False);
        Assert.That(EditorItemPosition.Set(plain, Vector2.One), Is.False);
    }
}

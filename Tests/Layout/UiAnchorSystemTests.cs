using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Systems.Layout;
using Position = Writer.Ecs.Position;

namespace Writer.Tests.Layout;

[TestFixture]
public sealed class UiAnchorSystemTests
{
    [Test]
    public void SticksToParentBottomWithoutParticipatingInLayout()
    {
        var store = new EntityStore();
        Entity parent = store.CreateEntity(
            new Rectangle(new Rect2(-500, -400, 1000, 800)));
        Entity child = store.CreateEntity(
            new Rectangle(new Rect2(-200, -100, 400, 200)),
            new Position(Vector2.Zero),
            new UiAnchor(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f)));
        child.AddTag<LayoutIgnore>();
        parent.AddChild(child);

        var root = new SystemRoot(store);
        root.Add(new UiAnchorSystem());
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(
            child.GetComponent<Position>().Value,
            Is.EqualTo(new Vector2(0, 300)));
    }
}

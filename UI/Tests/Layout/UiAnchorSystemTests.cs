using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Layout;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.UI.Layout;

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

    private static Vector2 Placed(UiAnchor anchor)
    {
        var store = new EntityStore();
        Entity parent = store.CreateEntity(
            new Rectangle(new Rect2(-500, -400, 1000, 800)));
        Entity child = store.CreateEntity(
            new Rectangle(new Rect2(-100, -50, 200, 100)),
            new Position(Vector2.Zero),
            anchor);
        child.AddTag<LayoutIgnore>();
        parent.AddChild(child);

        var root = new SystemRoot(store);
        root.Add(new UiAnchorSystem());
        root.Update(new UpdateTick(1f / 60f, 0));
        return child.GetComponent<Position>().Value;
    }

    [Test]
    public void AMarginKeepsACornerAnchorInFromBothEdges()
    {
        // Top-right: flush would be (400, -350); 12 in from the right and the top.
        Assert.That(
            Placed(new UiAnchor(new Vector2(1, 0), new Vector2(1, 0), margin: 12)),
            Is.EqualTo(new Vector2(388, -338)));
    }

    [Test]
    public void AMarginLeavesACentredAxisAlone()
    {
        // Bottom-centre: only the bottom edge pushes it in.
        Assert.That(
            Placed(new UiAnchor(new Vector2(0.5f, 1), new Vector2(0.5f, 1), margin: 10)),
            Is.EqualTo(new Vector2(0, 340)));
    }
}

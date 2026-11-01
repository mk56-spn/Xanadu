using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Layout;
using Xanadu.Systems.Rendering;
using Xanadu.Systems.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.UI;

[TestFixture]
public sealed class UiHiddenTests
{
    private EntityStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        UiTreeEvents.Ensure(_store);
    }

    private Entity Node(Entity? parent = null)
    {
        Entity node = _store.CreateEntity(
            new Position(Vector2.Zero),
            new Rectangle(new Rect2(-10, -10, 20, 20)));
        parent?.AddChild(node);
        return node;
    }

    private Entity Stack()
    {
        Entity stack = Node();
        stack.AddComponent(new UiContainer(StackDirection.Vertical));
        stack.RemoveTag<LayoutDirty>();
        return stack;
    }

    [Test]
    public void HidingTakesTheSubtreeOutOfQueries()
    {
        Entity root = Node();
        Entity child = Node(root);
        Entity grandchild = Node(child);

        UiLayout.SetHidden(root, true);

        Assert.That(_store.Query<Rectangle>().Count, Is.EqualTo(0));
        Assert.That(child.Enabled, Is.False);
        Assert.That(grandchild.Enabled, Is.False);

        UiLayout.SetHidden(root, false);
        Assert.That(_store.Query<Rectangle>().Count, Is.EqualTo(3));
    }

    [Test]
    public void ANodeHiddenOnItsOwnStaysHiddenWhenAnAncestorShows()
    {
        Entity root = Node();
        Entity child = Node(root);
        Entity below = Node(child);
        UiLayout.SetHidden(child, true);

        UiLayout.SetHidden(root, true);
        UiLayout.SetHidden(root, false);

        Assert.That(root.Enabled, Is.True);
        Assert.That(child.Enabled, Is.False);
        Assert.That(below.Enabled, Is.False);
    }

    [Test]
    public void AChildAddedUnderAHiddenNodeIsHidden()
    {
        Entity root = Node();
        UiLayout.SetHidden(root, true);

        Entity child = Node(root);
        Assert.That(child.Enabled, Is.False);

        UiLayout.SetHidden(root, false);
        Assert.That(child.Enabled, Is.True);
    }

    [Test]
    public void BufferedHidingAppliesOnPlayback()
    {
        Entity root = Node();
        Entity child = Node(root);
        CommandBuffer buffer = _store.GetCommandBuffer();

        UiLayout.SetHidden(root, buffer, true);
        Assert.That(child.Enabled, Is.True);

        buffer.Playback();
        Assert.That(child.Enabled, Is.False);
    }

    [Test]
    public void ChildrenComingAndGoingDirtyTheirContainer()
    {
        Entity stack = Stack();

        Entity child = Node(stack);
        Assert.That(stack.Tags.Has<LayoutDirty>(), Is.True);

        stack.RemoveTag<LayoutDirty>();
        child.DeleteEntity();
        Assert.That(stack.Tags.Has<LayoutDirty>(), Is.True);
    }

    [Test]
    public void HidingAChildDirtiesItsContainerAndDropsItFromTheStack()
    {
        Entity stack = Stack();
        stack.GetComponent<UiContainer>().AutoHeight = true;
        Node(stack);
        Entity second = Node(stack);
        var root = new SystemRoot(_store) { new ContainerLayoutSystem() };
        root.Update(new UpdateTick(1f / 60f, 0));
        float both = stack.GetComponent<Rectangle>().Bounds.Size.Y;

        UiLayout.SetHidden(second, true);
        Assert.That(stack.Tags.Has<LayoutDirty>(), Is.True);
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(stack.GetComponent<Rectangle>().Bounds.Size.Y, Is.LessThan(both));
    }

    [Test]
    public void CanvasItemsFollowTheirEntity()
    {
        Entity root = Node();
        Entity child = Node(root);
        child.AddComponent(new VisualRid(default));
        var canvas = new VisibilityCanvas();
        var systems = new SystemRoot(_store) { new CanvasVisibilitySystem(canvas) };

        UiLayout.SetHidden(root, true);
        systems.Update(new UpdateTick(1f / 60f, 0));
        Assert.That(canvas.Calls, Is.EqualTo(new[] { false }));
        Assert.That(child.Tags.Has<VisibilityDirty>(), Is.False);

        UiLayout.SetHidden(root, false);
        systems.Update(new UpdateTick(1f / 60f, 0));
        Assert.That(canvas.Calls, Is.EqualTo(new[] { false, true }));
    }

    [Test]
    public void AGreyedControlIsNotHiddenAndStaysInQueries()
    {
        Entity button = Node();
        UiLayout.SetDisabled(button, true);

        Assert.That(button.Enabled, Is.True);
        Assert.That(_store.Query<Rectangle>().Count, Is.EqualTo(1));
    }

    [TestCase(1, 0)]
    [TestCase(0, 1)]
    [TestCase(2, 0)]
    [TestCase(0, 2)]
    public void MovingAChildKeepsEverySibling(int from, int to)
    {
        Entity stack = Stack();
        Entity[] children = { Node(stack), Node(stack), Node(stack) };
        stack.RemoveTag<LayoutDirty>();

        UiLayout.MoveChild(stack, children[from], to);

        Assert.That(stack.ChildCount, Is.EqualTo(3));
        Assert.That(stack.ChildEntities[to], Is.EqualTo(children[from]));
        Assert.That(stack.Tags.Has<LayoutDirty>(), Is.True);
    }

    [Test]
    public void MovingAChildBetweenParentsTakesItsSubtree()
    {
        Entity from = Stack();
        Entity to = Stack();
        Entity child = Node(from);
        Entity below = Node(child);
        Node(to);

        UiLayout.MoveChild(to, child, 0);

        Assert.That(from.ChildCount, Is.EqualTo(0));
        Assert.That(to.ChildEntities[0], Is.EqualTo(child));
        Assert.That(below.Parent, Is.EqualTo(child));
    }

    [Test]
    public void DebugUiIsInheritedDownTheTree()
    {
        Entity root = Node();
        root.AddTag<DebugUi>();
        Entity child = Node(root);
        Entity below = Node(child);
        Entity other = Node();

        Assert.That(child.Tags.Has<DebugUi>(), Is.True);
        Assert.That(below.Tags.Has<DebugUi>(), Is.True);
        Assert.That(other.Tags.Has<DebugUi>(), Is.False);
    }

    [Test]
    public void DeleteSubtreeDeletesEveryDescendant()
    {
        Entity root = Node();
        Entity child = Node(root);
        Entity below = Node(child);

        UiLayout.DeleteSubtree(root);

        Assert.That(root.IsNull, Is.True);
        Assert.That(child.IsNull, Is.True);
        Assert.That(below.IsNull, Is.True);
    }

    private sealed class VisibilityCanvas : IRectCanvas
    {
        public List<bool> Calls { get; } = new();
        public void SetVisible(Rid canvas, bool visible) => Calls.Add(visible);
        public void SetTransform(Rid canvas, Transform2D transform) { }
        public void Clear(Rid canvas) { }
        public void DrawRect(Rid canvas, Rect2 bounds, Color color) { }
        public void DrawLine(Rid canvas, Vector2 from, Vector2 to, Color color, float width) { }
        public void DrawText(Rid canvas, Rect2 bounds, string text, Color color) { }
        public void DrawTextLeft(Rid canvas, Rect2 bounds, string text, Color color) { }
        public void DrawTextRight(Rid canvas, Rect2 bounds, string text, Color color) { }
    }
}

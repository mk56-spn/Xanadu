using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Editor;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class SelectionAlignTests
{
    private static readonly Rect2 Bounds = new(100, 100, 400, 300);
    private static readonly Rect2 Item = new(200, 150, 100, 50);

    [TestCase(AlignEdge.Left, -100, 0)]
    [TestCase(AlignEdge.CenterX, 50, 0)]
    [TestCase(AlignEdge.Right, 200, 0)]
    [TestCase(AlignEdge.Top, 0, -50)]
    [TestCase(AlignEdge.CenterY, 0, 75)]
    [TestCase(AlignEdge.Bottom, 0, 200)]
    public void AnItemMovesOntoTheSelectionsLine(AlignEdge edge, float x, float y)
    {
        Assert.That(SelectionAlign.Offset(Item, Bounds, edge), Is.EqualTo(new Vector2(x, y)));
    }

    [Test]
    public void APointLinesUpByItsPosition()
    {
        var note = new Rect2(new Vector2(300, 300), Vector2.Zero);
        Assert.That(SelectionAlign.Offset(note, Bounds, AlignEdge.Right), Is.EqualTo(new Vector2(200, 0)));
    }

    [Test]
    public void BlocksLineUpOnTheirLeftEdgesAndBottoms()
    {
        var store = new EntityStore();
        Entity wide = EditorItems.Create(store, new Block(new Vector2(100, 100), new Vector2(200, 50)));
        Entity small = EditorItems.Create(store, new Block(new Vector2(300, 300), new Vector2(50, 50)));
        Entity[] selection = { wide, small };

        Assert.That(SelectionAlign.Apply(selection, AlignEdge.Left), Is.True);
        Assert.That(small.GetComponent<Block>().Position, Is.EqualTo(new Vector2(100, 300)));
        Assert.That(wide.GetComponent<Block>().Position, Is.EqualTo(new Vector2(100, 100)));

        Assert.That(SelectionAlign.Apply(selection, AlignEdge.Bottom), Is.True);
        Assert.That(wide.GetComponent<Block>().Position, Is.EqualTo(new Vector2(100, 300)));

        Assert.That(SelectionAlign.Apply(selection, AlignEdge.Bottom), Is.False, "already lined up");
    }

    [Test]
    public void OneItemHasNothingToLineUpWith()
    {
        var store = new EntityStore();
        Entity block = EditorItems.Create(store, new Block(new Vector2(100, 100), new Vector2(50, 50)));
        Assert.That(SelectionAlign.CanAlign(new[] { block }), Is.False);
    }
}

using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Editor;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class BlockEditingTests
{
    private static readonly Playfield Field = new(1000, 1000);
    private static readonly PlayfieldView View =
        new(Vector2.Zero, 1f, new Vector2(1000, 1000));

    private static Block Platform => new(new Vector2(400, 500), new Vector2(200, 100));

    [Test]
    public void HandlesSitAtTheMiddleOfEachSide()
    {
        Block block = Platform;

        Assert.That(
            BlockEditing.HandleCenter(block, BlockHandle.Left),
            Is.EqualTo(new Vector2(400, 550)));
        Assert.That(
            BlockEditing.HandleCenter(block, BlockHandle.Right),
            Is.EqualTo(new Vector2(600, 550)));
        Assert.That(
            BlockEditing.HandleCenter(block, BlockHandle.Top),
            Is.EqualTo(new Vector2(500, 500)));
        Assert.That(
            BlockEditing.HandleCenter(block, BlockHandle.Bottom),
            Is.EqualTo(new Vector2(500, 600)));
    }

    [Test]
    public void ClickingAHandlePicksThatSide()
    {
        Assert.That(
            BlockEditing.TryPick(Platform, View, new Vector2(604, 548), out BlockHandle handle),
            Is.True);
        Assert.That(handle, Is.EqualTo(BlockHandle.Right));

        Assert.That(
            BlockEditing.TryPick(Platform, View, new Vector2(500, 540), out _),
            Is.False);
    }

    [Test]
    public void ResizingTheRightEdgeSnapsToTheGrid()
    {
        Block resized = BlockEditing.Resize(
            Platform,
            BlockHandle.Right,
            new Vector2(688, 550),
            Field);

        Assert.That(resized.Position, Is.EqualTo(new Vector2(400, 500)));
        Assert.That(resized.Size, Is.EqualTo(new Vector2(300, 100)));
    }

    [Test]
    public void ResizingTheLeftEdgeKeepsTheRightEdgePut()
    {
        Block resized = BlockEditing.Resize(
            Platform,
            BlockHandle.Left,
            new Vector2(320, 550),
            Field);

        Assert.That(resized.Position, Is.EqualTo(new Vector2(300, 500)));
        Assert.That(resized.Size, Is.EqualTo(new Vector2(300, 100)));
        Assert.That(resized.End.X, Is.EqualTo(600));
    }

    [Test]
    public void ResizingTheTopEdgeSnapsToTheGrid()
    {
        Block resized = BlockEditing.Resize(
            Platform,
            BlockHandle.Top,
            new Vector2(500, 380),
            Field);

        Assert.That(resized.Position, Is.EqualTo(new Vector2(400, 400)));
        Assert.That(resized.Size, Is.EqualTo(new Vector2(200, 200)));
    }

    [Test]
    public void ResizingTheBottomEdgeSnapsToTheGrid()
    {
        Block resized = BlockEditing.Resize(
            Platform,
            BlockHandle.Bottom,
            new Vector2(500, 744),
            Field);

        Assert.That(resized.Position, Is.EqualTo(new Vector2(400, 500)));
        Assert.That(resized.Size, Is.EqualTo(new Vector2(200, 250)));
    }

    [Test]
    public void ABlockCannotBeResizedBelowOneCell()
    {
        Block resized = BlockEditing.Resize(
            Platform,
            BlockHandle.Right,
            new Vector2(410, 550),
            Field);

        Assert.That(resized.Size.X, Is.EqualTo(BlockGrid.Cell(Field).X));

        Block upwards = BlockEditing.Resize(
            Platform,
            BlockHandle.Top,
            new Vector2(500, 590),
            Field);
        Assert.That(upwards.Size.Y, Is.EqualTo(BlockGrid.Cell(Field).Y));
        Assert.That(upwards.End.Y, Is.EqualTo(600));
    }

    [Test]
    public void MovingSnapsBothEdgesToTheGrid()
    {
        Block moved = BlockEditing.Move(
            Platform,
            new Vector2(60, 40),
            Field);

        Assert.That(moved.Position, Is.EqualTo(new Vector2(450, 550)));
        Assert.That(moved.Size, Is.EqualTo(Platform.Size));
    }

    [Test]
    public void MovingStopsAtTheFieldEdge()
    {
        Block moved = BlockEditing.Move(
            Platform,
            new Vector2(900, -900),
            Field);

        Assert.That(moved.Position, Is.EqualTo(new Vector2(800, 0)));
    }
}

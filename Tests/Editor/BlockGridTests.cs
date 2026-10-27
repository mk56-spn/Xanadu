using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Editor;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class BlockGridTests
{
    private static readonly Playfield Square = new(1000, 1000);

    [Test]
    public void CellMatchesTheDrawnGrid()
    {
        Assert.That(BlockGrid.Cell(Square), Is.EqualTo(new Vector2(100, 100)));
        Assert.That(
            BlockGrid.Cell(new Playfield(800, 400)),
            Is.EqualTo(new Vector2(80, 40)));
    }

    [Test]
    public void SnapRoundsToTheNearestGridLine()
    {
        Assert.That(BlockGrid.Snap(44, 100), Is.EqualTo(0));
        Assert.That(BlockGrid.Snap(45, 100), Is.EqualTo(0));
        Assert.That(BlockGrid.Snap(56, 100), Is.EqualTo(100));
        Assert.That(BlockGrid.Snap(150, 100), Is.EqualTo(200));
        Assert.That(
            BlockGrid.Snap(new Vector2(237, 611), new Vector2(100, 100)),
            Is.EqualTo(new Vector2(200, 600)));
    }

    [Test]
    public void DefaultBlockIsTwoCellsWideAndOneTall()
    {
        Assert.That(
            BlockGrid.DefaultSize(Square),
            Is.EqualTo(new Vector2(200, 100)));
    }

    [Test]
    public void PlaceAtCentresTheBlockOnTheGrid()
    {
        Vector2 size = BlockGrid.DefaultSize(Square);

        Assert.That(
            BlockGrid.PlaceAt(new Vector2(520, 520), size, Square),
            Is.EqualTo(new Vector2(400, 500)));
    }

    [Test]
    public void PlaceAtKeepsTheBlockInsideTheField()
    {
        Vector2 size = BlockGrid.DefaultSize(Square);

        Assert.That(
            BlockGrid.PlaceAt(new Vector2(4, 4), size, Square),
            Is.EqualTo(Vector2.Zero));
        Assert.That(
            BlockGrid.PlaceAt(new Vector2(996, 996), size, Square),
            Is.EqualTo(new Vector2(800, 900)));
    }
}

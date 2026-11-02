using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Editor;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class GridPlacementTests
{
    private static readonly Playfield Field = new(1000, 1000);
    private static readonly PlayfieldView View = new(Vector2.Zero, 1f, new Vector2(1000, 1000));
    private static readonly Rect2 Platform = new(new Vector2(400, 500), new Vector2(200, 100));

    [Test]
    public void CellMatchesTheDrawnGrid()
    {
        Assert.That(GridPlacement.Cell(Field), Is.EqualTo(new Vector2(50, 50)));
        Assert.That(GridPlacement.Cell(new Playfield(800, 400)), Is.EqualTo(new Vector2(40, 20)));
    }

    [Test]
    public void NewItemsAreTheirGridSizes()
    {
        Assert.That(GridPlacement.NewBlockSize(Field), Is.EqualTo(new Vector2(100, 50)));
        Assert.That(GridPlacement.NewBlockSize(Field, 90f), Is.EqualTo(new Vector2(50, 100)));
        Assert.That(GridPlacement.NewSpikeSize(Field), Is.EqualTo(new Vector2(50, 50)));
    }

    [Test]
    public void BlocksAndSpikesPlaceTheSameWay()
    {
        Assert.That(
            GridPlacement.Place(new Vector2(520, 520), new Vector2(100, 50), Field),
            Is.EqualTo(new Vector2(450, 500)));
        Assert.That(
            GridPlacement.Place(new Vector2(370, 640), new Vector2(50, 50), Field),
            Is.EqualTo(new Vector2(350, 600)));
    }

    [Test]
    public void PlacingKeepsTheItemInsideTheField()
    {
        Vector2 size = new(100, 50);
        Assert.That(GridPlacement.Place(new Vector2(4, 4), size, Field), Is.EqualTo(Vector2.Zero));
        Assert.That(GridPlacement.Place(new Vector2(996, 996), size, Field), Is.EqualTo(new Vector2(900, 950)));
    }

    [Test]
    public void SnapOffPlacesExactly()
    {
        Assert.That(
            GridPlacement.Place(new Vector2(523, 517), new Vector2(50, 50), Field, snap: false),
            Is.EqualTo(new Vector2(498, 492)));
    }

    [Test]
    public void HandlesSitAtTheMiddleOfEachSide()
    {
        Assert.That(GridPlacement.HandleCenter(Platform, GridHandle.Left), Is.EqualTo(new Vector2(400, 550)));
        Assert.That(GridPlacement.HandleCenter(Platform, GridHandle.Right), Is.EqualTo(new Vector2(600, 550)));
        Assert.That(GridPlacement.HandleCenter(Platform, GridHandle.Top), Is.EqualTo(new Vector2(500, 500)));
        Assert.That(GridPlacement.HandleCenter(Platform, GridHandle.Bottom), Is.EqualTo(new Vector2(500, 600)));
    }

    [Test]
    public void HandlesTurnWithTheItem()
    {
        Vector2 top = GridPlacement.HandleCenter(new Rect2(400, 400, 100, 100), GridHandle.Top, 90f);
        Assert.That(top.IsEqualApprox(new Vector2(500, 450)), Is.True);
    }

    [Test]
    public void ClickingAHandlePicksThatSide()
    {
        Assert.That(
            GridPlacement.TryPickHandle(Platform, 0f, View, new Vector2(604, 548), out GridHandle handle),
            Is.True);
        Assert.That(handle, Is.EqualTo(GridHandle.Right));
        Assert.That(GridPlacement.TryPickHandle(Platform, 0f, View, new Vector2(500, 540), out _), Is.False);
    }

    [TestCase(GridHandle.Right, 688, 550, 400, 500, 300, 100)]
    [TestCase(GridHandle.Left, 320, 550, 300, 500, 300, 100)]
    [TestCase(GridHandle.Top, 500, 380, 400, 400, 200, 200)]
    [TestCase(GridHandle.Bottom, 500, 744, 400, 500, 200, 250)]
    public void ResizingSnapsTheDraggedEdgeAndKeepsTheOther(
        GridHandle handle, float x, float y, float left, float top, float width, float height)
    {
        Assert.That(
            GridPlacement.Resize(Platform, handle, new Vector2(x, y), Field),
            Is.EqualTo(new Rect2(left, top, width, height)));
    }

    [Test]
    public void AnItemCannotBeResizedBelowOneCell()
    {
        Assert.That(GridPlacement.Resize(Platform, GridHandle.Right, new Vector2(410, 550), Field).Size.X, Is.EqualTo(50));

        Rect2 upwards = GridPlacement.Resize(Platform, GridHandle.Top, new Vector2(500, 590), Field);
        Assert.That(upwards.Size.Y, Is.EqualTo(50));
        Assert.That(upwards.End.Y, Is.EqualTo(600));
    }

    [Test]
    public void ResizingATurnedItemKeepsItsFarEdgeWhereItIsOnTheField()
    {
        // Turned a quarter, the box's "right" side faces down the field.
        var start = new Rect2(400, 400, 100, 100);
        Vector2 keptBefore = GridPlacement.HandleCenter(start, GridHandle.Left, 90f);

        Rect2 resized = GridPlacement.Resize(start, GridHandle.Right, new Vector2(450, 600), Field, snap: true, degrees: 90f);

        Assert.That(resized.Size.X, Is.EqualTo(200).Within(0.01f), "stretches along its own axis");
        Assert.That(resized.Size.Y, Is.EqualTo(100).Within(0.01f));
        Assert.That(GridPlacement.HandleCenter(resized, GridHandle.Left, 90f).IsEqualApprox(keptBefore), Is.True);
    }

    [Test]
    public void MovingSnapsAndStopsAtTheFieldEdge()
    {
        Assert.That(GridPlacement.Move(Platform.Position, Platform.Size, new Vector2(60, 40), Field), Is.EqualTo(new Vector2(450, 550)));
        Assert.That(GridPlacement.Move(Platform.Position, Platform.Size, new Vector2(900, -900), Field), Is.EqualTo(new Vector2(800, 0)));
    }
}

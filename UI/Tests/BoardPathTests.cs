using NUnit.Framework;
using Xanadu.Ui.Editing;

namespace Xanadu.Ui.Tests;

[TestFixture]
public sealed class BoardPathTests
{
    [Test]
    public void LineWithNothingInTheWayIsStraight()
    {
        var board = new Board();
        BoardBox a = board.Add(new Rect2(0, 0, 100, 60)), b = board.Add(new Rect2(400, 0, 100, 60));
        board.Link(a.Id, b.Id);

        Assert.That(board.Path(board.Links[0]), Has.Count.EqualTo(2));
    }

    [Test]
    public void LineBendsRoundABoxInItsWay()
    {
        var board = new Board();
        BoardBox a = board.Add(new Rect2(0, 0, 100, 60)), b = board.Add(new Rect2(400, 0, 100, 60));
        BoardBox between = board.Add(new Rect2(200, -20, 100, 100));
        board.Link(a.Id, b.Id);

        var path = board.Path(board.Links[0])!;

        Assert.That(path, Has.Count.GreaterThan(2));
        for (int i = 1; i < path.Count; i++)
            Assert.That(Board.Crosses(path[i - 1], path[i], between.Rect, out _), Is.False);
    }

    [Test]
    public void LineRunsStraightOverAFrame()
    {
        var board = new Board();
        BoardBox a = board.Add(new Rect2(0, 0, 100, 60)), b = board.Add(new Rect2(600, 0, 100, 60));
        board.Add(new Rect2(200, -100, 300, 300));
        board.Add(new Rect2(220, 120, 60, 40));
        board.Link(a.Id, b.Id);

        Assert.That(board.Path(board.Links[0]), Has.Count.EqualTo(2));
    }

    [Test]
    public void BentLineIsPickedOnItsBend()
    {
        var board = new Board();
        BoardBox a = board.Add(new Rect2(0, 0, 100, 60)), b = board.Add(new Rect2(400, 0, 100, 60));
        board.Add(new Rect2(200, -20, 100, 100));
        board.Link(a.Id, b.Id);

        var path = board.Path(board.Links[0])!;

        Assert.That(board.LinkAt((path[1] + path[2]) / 2, 2), Is.EqualTo(board.Links[0]));
        Assert.That(board.LinkAt(new Vector2(250, 30), 2), Is.Null);
    }

    [Test]
    public void TwoLinesRoundTheSameBoxKeepApart()
    {
        var board = new Board();
        BoardBox a = board.Add(new Rect2(0, 0, 100, 60)), b = board.Add(new Rect2(400, 0, 100, 60));
        BoardBox c = board.Add(new Rect2(0, 20, 100, 60)), d = board.Add(new Rect2(400, 20, 100, 60));
        board.Add(new Rect2(200, -20, 100, 100));
        board.Link(a.Id, b.Id);
        board.Link(c.Id, d.Id);

        var paths = board.Paths();

        Assert.That(paths[0]![1], Is.Not.EqualTo(paths[1]![1]));
    }

    [Test]
    public void LinesTurnOnlyAtRightAngles()
    {
        var board = new Board();
        BoardBox a = board.Add(new Rect2(0, 0, 100, 60)), b = board.Add(new Rect2(300, 200, 100, 60));
        BoardBox c = board.Add(new Rect2(-200, 400, 100, 60));
        board.Link(a.Id, b.Id);
        board.Link(a.Id, c.Id);
        board.Link(b.Id, c.Id);

        foreach (var path in board.Paths())
            for (int i = 1; i < path!.Count; i++)
                Assert.That(path[i - 1].X == path[i].X || path[i - 1].Y == path[i].Y, Is.True);
    }

    [Test]
    public void OppositeLinesBetweenTwoBoxesAreOneLine()
    {
        var board = new Board();
        BoardBox a = board.Add(new Rect2(0, 0, 100, 60)), b = board.Add(new Rect2(400, 0, 100, 60));
        board.Link(a.Id, b.Id);
        board.Link(b.Id, a.Id);

        var paths = board.Paths();

        Assert.That(paths[0]![0].X, Is.EqualTo(100).Within(0.01f));
        Assert.That(paths[1], Is.Null);
        Assert.That(board.TwoWay(board.Links[0]), Is.True);
    }
}

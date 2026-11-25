using System.Collections.Generic;
using NUnit.Framework;
using Xanadu.Ui.Editing;

namespace Xanadu.Ui.Tests;

/// <summary>Where lines cross, the later one hops over the earlier.</summary>
[TestFixture]
public sealed class LineHopsTests
{
    [Test]
    public void TheLaterLineHopsWhereTheyCross()
    {
        List<List<Vector2>?> paths =
        [
            [new(50, 0), new(50, 100)],
            [new(0, 50), new(100, 50)],
        ];

        List<List<LineHop>> hops = LineHops.Of(paths, clear: 5);

        Assert.That(hops[0], Is.Empty);
        Assert.That(hops[1], Is.EqualTo(new[] { new LineHop(0, 50) }));
    }

    [Test]
    public void CrossingsNearAnEndAndLinesThatMissAreNotHops()
    {
        List<List<Vector2>?> paths =
        [
            [new(0, 0), new(100, 0)],
            // Leaves from where the first one starts: a shared end, not a crossing.
            [new(0, 0), new(100, 100)],
            null,
            [new(0, 200), new(100, 200)],
        ];

        List<List<LineHop>> hops = LineHops.Of(paths, clear: 5);

        Assert.That(hops[1], Is.Empty);
        Assert.That(hops[2], Is.Empty);
        Assert.That(hops[3], Is.Empty);
    }

    [Test]
    public void HopsComeInOrderAlongEachSegment()
    {
        List<List<Vector2>?> paths =
        [
            [new(80, 0), new(80, 100)],
            [new(20, 0), new(20, 100)],
            [new(0, 50), new(100, 50)],
        ];

        List<List<LineHop>> hops = LineHops.Of(paths, clear: 5);

        Assert.That(hops[2], Is.EqualTo(new[] { new LineHop(0, 20), new LineHop(0, 80) }));
    }
}

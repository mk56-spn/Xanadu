using System.Collections.Generic;
using NUnit.Framework;
using Xanadu.Layering;

namespace Xanadu.Tests.Layering;

[TestFixture]
public sealed class DrawStackTests
{
    [Test]
    public void SortingListsTheFrontFirst()
    {
        var rows = new List<DrawSlot> { new(0, 0, 0), new(2, 0, 0), new(0, 1, 0), new(0, 0, 1) };
        DrawStack.SortFrontFirst(rows, static slot => slot);
        Assert.That(rows, Is.EqualTo(new[] { new DrawSlot(2, 0, 0), new(0, 1, 0), new(0, 0, 1), new(0, 0, 0) }));
    }

    [Test]
    public void ForwardPassesTheRowAboveAndStopsAtTheEnds()
    {
        Assert.That(DrawStack.Neighbour(2, 1, 4), Is.EqualTo(1));
        Assert.That(DrawStack.Neighbour(2, -1, 4), Is.EqualTo(3));
        Assert.That(DrawStack.Neighbour(0, 1, 4), Is.EqualTo(-1));
        Assert.That(DrawStack.Neighbour(3, -1, 4), Is.EqualTo(-1));
        Assert.That(DrawStack.Neighbour(-1, 1, 4), Is.EqualTo(-1));
    }

    [Test]
    public void LayersRuleSwapsLayersWithANeighbourOnAnotherLayer()
    {
        Assert.That(DrawStack.TryMove(new(0, 0, 1), new(5, 0, 0), 1, DrawStackRule.Layers,
            out DrawSlot row, out DrawSlot neighbour), Is.True);
        Assert.That(row.Layer, Is.EqualTo(5));
        Assert.That(neighbour.Layer, Is.EqualTo(0));
    }

    [Test]
    public void LayersRuleStepsOneLayerPastANeighbourOnTheSameLayer()
    {
        DrawStack.TryMove(new(2, 0, 0), new(2, 0, 1), -1, DrawStackRule.Layers,
            out DrawSlot row, out DrawSlot neighbour);
        Assert.That(row.Layer, Is.EqualTo(1));
        Assert.That(neighbour, Is.EqualTo(new DrawSlot(2, 0, 1)));
    }

    [Test]
    public void LayersRuleStaysInsideTheRange()
    {
        DrawStack.TryMove(new(DrawOrder.MaxLayer, 0, 0), new(DrawOrder.MaxLayer, 0, 1), 1, DrawStackRule.Layers,
            out DrawSlot row, out _);
        Assert.That(row.Layer, Is.EqualTo(DrawOrder.MaxLayer));
    }

    [Test]
    public void ZRuleSwapsZWithANeighbourOnTheSameLayer()
    {
        Assert.That(DrawStack.TryMove(new(-1, 3, 0), new(-1, 7, 1), 1, DrawStackRule.ZInLayer,
            out DrawSlot row, out DrawSlot neighbour), Is.True);
        Assert.That(row, Is.EqualTo(new DrawSlot(-1, 7, 0)));
        Assert.That(neighbour, Is.EqualTo(new DrawSlot(-1, 3, 1)));
    }

    [Test]
    public void ZRuleStepsPastANeighbourItIsTiedWith()
    {
        DrawStack.TryMove(new(-1, 4, 0), new(-1, 4, 1), 1, DrawStackRule.ZInLayer,
            out DrawSlot row, out DrawSlot neighbour);
        Assert.That(row.CompareTo(neighbour), Is.GreaterThan(0));
        Assert.That(neighbour, Is.EqualTo(new DrawSlot(-1, 4, 1)));
    }

    [Test]
    public void ZRuleNeverChangesLayer()
    {
        Assert.That(DrawStack.TryMove(new(-1, 0, 0), new(1, 0, 0), 1, DrawStackRule.ZInLayer,
            out DrawSlot row, out DrawSlot neighbour), Is.False);
        Assert.That(row, Is.EqualTo(new DrawSlot(-1, 0, 0)));
        Assert.That(neighbour, Is.EqualTo(new DrawSlot(1, 0, 0)));
    }
}

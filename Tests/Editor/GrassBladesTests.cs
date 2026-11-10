using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Playfields;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class GrassBladesTests
{
    private static (List<Vector2> Points, List<Color> Colors) Build(GrassPatch patch, float seconds)
    {
        var points = new List<Vector2>();
        var colors = new List<Color>();
        GrassBlades.Build(patch, seconds, points, colors);
        return (points, colors);
    }

    [Test]
    public void EachBladeIsAFewTrianglesWithAColourPerPoint()
    {
        var patch = new GrassPatch(new Vector2(500, 500), 7) { Width = 100, Density = 40 };

        (List<Vector2> points, List<Color> colors) = Build(patch, 0f);

        Assert.That(patch.BladeCount, Is.EqualTo(40));
        Assert.That(points, Has.Count.EqualTo(40 * GrassBlades.TrianglesPerBlade * 3));
        Assert.That(colors, Has.Count.EqualTo(points.Count));
    }

    [Test]
    public void BladesStandOnTheBaseLineAndReachNoHigherThanTheLength()
    {
        var patch = new GrassPatch(new Vector2(500, 500), 7) { Width = 200, Length = 60, Sway = 0 };

        (List<Vector2> points, _) = Build(patch, 3f);

        Assert.That(points.Max(p => p.Y), Is.LessThanOrEqualTo(500.01f));
        Assert.That(points.Min(p => p.Y), Is.GreaterThanOrEqualTo(500f - 60f - 0.01f));
        Assert.That(points.Min(p => p.X), Is.GreaterThan(400f - 20f));
        Assert.That(points.Max(p => p.X), Is.LessThan(600f + 20f));
    }

    [Test]
    public void SwayMovesTheBladesOverTimeAndNoSwayHoldsThemStill()
    {
        var swaying = new GrassPatch(new Vector2(500, 500), 7) { Sway = 20 };
        var still = swaying with { Sway = 0 };

        Assert.That(Build(swaying, 0f).Points, Is.Not.EqualTo(Build(swaying, 0.5f).Points));
        Assert.That(Build(still, 0f).Points, Is.EqualTo(Build(still, 0.5f).Points));
    }

    [Test]
    public void MovingAPatchMovesTheSameBlades()
    {
        var patch = new GrassPatch(new Vector2(500, 500), 7) { Sway = 0 };
        var moved = patch with { Position = new Vector2(530, 480) };

        List<Vector2> before = Build(patch, 0f).Points;
        List<Vector2> after = Build(moved, 0f).Points;

        for (int i = 0; i < before.Count; i++)
            Assert.That(after[i].DistanceTo(before[i] + new Vector2(30, -20)), Is.LessThan(0.01f));
    }

    [Test]
    public void NoVariationKeepsEveryTipTheGrassColour()
    {
        var patch = new GrassPatch(new Vector2(500, 500), 7) { Variation = 0, Color = new Color(0.2f, 0.6f, 0.3f) };

        (_, List<Color> colors) = Build(patch, 0f);

        Assert.That(colors.Max(c => c.G), Is.EqualTo(0.6f).Within(0.001f));
    }
}

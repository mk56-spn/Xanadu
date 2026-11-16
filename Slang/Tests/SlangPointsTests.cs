using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NUnit.Framework;

namespace Xanadu.Slang.Tests;

/// <summary>Which <c>float2(x, y)</c>s the editor offers to drag on the preview, read with the shipped library's parameter names.</summary>
[TestFixture]
public sealed class SlangPointsTests
{
    private SlangCompletions _completions = null!;

    [OneTimeSetUp]
    public void Build() =>
        _completions = SlangCompletions.FromSources(ShippedLibrary.Sources, ShaderConstants.FromSources(ShippedLibrary.Sources), []);

    private List<PointLiteral> Find(params string[] lines) => SlangPoints.Find(lines, _completions.ParameterNames);

    [Test]
    public void TheLibraryNamesItsParameters()
    {
        Assert.That(_completions.ParameterNames("moved", 0), Is.EqualTo(new[] { "offset" }));
        Assert.That(_completions.ParameterNames("Line", 1), Is.EqualTo(new[] { "b" }));
        Assert.That(_completions.ParameterNames("Box", 0), Does.Contain("size"));
        Assert.That(_completions.ParameterNames("nothingLikeThis", 0), Is.Empty);
    }

    [Test]
    public void APlaceGivenToTheLibraryIsAPoint()
    {
        List<PointLiteral> points = Find(
            "let dot = Circle(30).moved(float2(100, -20));",
            "let line = Line(float2(-40, 0), float2(40.5, 10), 3);",
            "float a = from.angleTo(float2(7, 8));");
        Assert.That(points.Select(p => p.Name), Is.EqualTo(new[] { "offset", "a", "b", "target" }));
        Assert.That(points[0].Value, Is.EqualTo(new Vector2(100, -20)));
        Assert.That(points[2].Value, Is.EqualTo(new Vector2(40.5f, 10)));
        Assert.That((points[0].Line, points[0].Start, points[0].End), Is.EqualTo((0, 27, 43)));
    }

    [Test]
    public void ASizeOrARangeIsNot()
    {
        Assert.That(Find(
            "let b = Box(float2(80, 10), 4);",
            "float r = remapped(x, float2(-1, 1), float2(0, 1));",
            "let g = Grid(float2(20, 20));",
            "c.anchor(float2(0.5, 0.5));"), Is.Empty);
    }

    [Test]
    public void AnythingWorkedOutIsNot()
    {
        Assert.That(Find(
            "let a = Circle(4).moved(float2(i * 20, 0));",
            "let b = Circle(4).moved(float2(10, 0) * 2);",
            "let c = Circle(4).moved(offset + float2(10, 0));",
            "float d = float2(3, 4).x;",
            "let e = Circle(4).moved(points[float2(1, 2)]);"), Is.Empty);
    }

    [Test]
    public void AVariableIsAPointUnlessItsNameSaysOtherwise()
    {
        List<PointLiteral> points = Find(
            "float2 target = float2(40, 30);",
            "let eye = float2(-80, 0);",
            "light = float2(1, 2);",
            "float2 size = float2(80, 10);",
            "float2 tileSpacing = float2(4, 4);",
            "float2 to = float2(1, 0);");
        Assert.That(points.Select(p => p.Name), Is.EqualTo(new[] { "target", "eye", "light" }));
    }

    [Test]
    public void ACommentOrAnUnknownCallIsNot()
    {
        Assert.That(Find(
            "// let a = Circle(4).moved(float2(10, 0));",
            "let b = mine(float2(10, 0));"), Is.Empty);
    }

    [Test]
    public void TheCaretFindsThePointItIsOn()
    {
        string[] code = ["x;", "let line = Line(float2(-40, 0), float2(40, 10));"];
        Assert.That(SlangPoints.At(code, 1, 18, _completions.ParameterNames)?.Name, Is.EqualTo("a"));
        Assert.That(SlangPoints.At(code, 1, 30, _completions.ParameterNames)?.Name, Is.EqualTo("a"));
        Assert.That(SlangPoints.At(code, 1, 34, _completions.ParameterNames)?.Name, Is.EqualTo("b"));
        Assert.That(SlangPoints.At(code, 1, 34, _completions.ParameterNames)?.Line, Is.EqualTo(1));
        Assert.That(SlangPoints.At(code, 1, 4, _completions.ParameterNames), Is.Null);
    }

    [Test]
    public void ANewValueIsWrittenInWholePixels()
    {
        Assert.That(SlangPoints.Text(new Vector2(40.4f, -12.6f)), Is.EqualTo("float2(40, -13)"));
        Assert.That(SlangPoints.Text(new Vector2(-0.2f, 0)), Is.EqualTo("float2(0, 0)"));
    }
}

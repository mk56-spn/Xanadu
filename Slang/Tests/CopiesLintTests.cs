using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Xanadu.Slang.Tests;

/// <summary>XS009, and XS008 standing aside: a stack that draws copies of its quad ([Draws]).</summary>
public sealed class CopiesLintTests
{
    private static readonly IReadOnlySet<string> Types = new HashSet<string>(SlangSyntax.BuiltInTypes);

    [Test]
    public void AGridOnAStackThatDrawsCopiesIsFlaggedOnTheGrid()
    {
        SlangProblem problem = SlangLint.Check("""
            [Draws] RWStructuredBuffer<uint> drawn;
            void build() { }
            [Grid(72, 72)] ItemVertex deform(ItemVertex v) { return v; }
            """, Types).Single();
        Assert.That(problem.Message, Does.StartWith("XS009"));
        Assert.That((problem.Line, problem.Column), Is.EqualTo((3, 1)));
    }

    [Test]
    public void AGridWithoutCopiesIsFine()
    {
        Assert.That(SlangLint.Check("[Grid(96, 54)] ItemVertex deform(ItemVertex v) { return v; }", Types), Is.Empty);
    }

    [Test]
    public void ABendThatPlacesCopiesNeedsNoGrid()
    {
        Assert.That(SlangLint.Check("""
            [Draws] RWStructuredBuffer<uint> drawn;
            void build() { }
            ItemVertex deform(ItemVertex v) { v.position = v.position.wave(2); return v; }
            """, Types), Is.Empty);
    }
}

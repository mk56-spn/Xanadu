using NUnit.Framework;
using Xanadu.Ui.Editing;

namespace Xanadu.Ui.Tests;

/// <summary>Folding a text's brace blocks to their headers: which lines fold, stepping over them, and keeping them through edits.</summary>
[TestFixture]
public sealed class TextFoldsTests
{
    private const string Code = """
        struct Spark {
            float age;
        };

        void step(uint i)
        {
            if (i > 0)
            {
                i--;
            }
            // a { in a comment
            string s = "}";
        }
        """;

    private static (TextBuffer Buffer, TextFolds Folds) Folded(string text)
    {
        var buffer = new TextBuffer();
        buffer.Load(text);
        var folds = new TextFolds();
        folds.Update(buffer, "//");
        return (buffer, folds);
    }

    [Test]
    public void ABlockFoldsToTheLineItsBraceOpensOnOrTheHeaderAboveALoneBrace()
    {
        (_, TextFolds folds) = Folded(Code);

        Assert.That(folds.CanFold(0) && folds.EndOf(0) == 2, "a struct with its brace on the header line");
        Assert.That(folds.CanFold(4) && folds.EndOf(4) == 12, "a method with its brace under it folds from the method's line");
        Assert.That(folds.CanFold(6) && folds.EndOf(6) == 9);
        Assert.That(folds.CanFold(5), Is.False, "the lone brace isn't a header of its own");
    }

    [Test]
    public void BracesInCommentsAndQuotesDontCount()
    {
        (_, TextFolds folds) = Folded(Code);

        Assert.That(folds.EndOf(4), Is.EqualTo(12));
        Assert.That(folds.BlockAt(11), Is.EqualTo(4));
    }

    [Test]
    public void SteppingSkipsAFoldedBlock()
    {
        (TextBuffer buffer, TextFolds folds) = Folded(Code);
        folds.Fold(0);

        Assert.That(folds.Next(0), Is.EqualTo(3));
        Assert.That(folds.Previous(3), Is.EqualTo(0));
        Assert.That(folds.Hiding(1), Is.EqualTo(0));
        Assert.That(folds.Step(4, -2, buffer.Lines.Count), Is.EqualTo(0));
    }

    [Test]
    public void TheOutermostFoldHidesANestedOne()
    {
        (_, TextFolds folds) = Folded(Code);
        folds.Fold(6);
        folds.Fold(4);

        Assert.That(folds.Hiding(8), Is.EqualTo(4));
        Assert.That(folds.Previous(13), Is.EqualTo(4));
        folds.Show(8);
        Assert.That(folds.Folded, Is.Empty);
    }

    [Test]
    public void AFoldMovesWithItsLinesWhenTextGoesInAbove()
    {
        (TextBuffer buffer, TextFolds folds) = Folded(Code);
        folds.Fold(4);
        buffer.Place(0, 0);
        buffer.Insert("// new\n// lines\n");
        folds.Update(buffer, "//");

        Assert.That(folds.Folded, Is.EqualTo(new[] { 6 }));
        Assert.That(folds.EndOf(6), Is.EqualTo(14));
    }

    [Test]
    public void AnEditInsideAFoldLeavesItFolded()
    {
        (TextBuffer buffer, TextFolds folds) = Folded(Code);
        folds.Fold(4);
        buffer.Place(8, 0);
        buffer.Insert("    i++;\n");
        folds.Update(buffer, "//");

        Assert.That(folds.Folded, Is.EqualTo(new[] { 4 }));
        Assert.That(folds.EndOf(4), Is.EqualTo(13));
    }

    [Test]
    public void AFoldWhoseBlockIsGoneOpens()
    {
        (TextBuffer buffer, TextFolds folds) = Folded(Code);
        folds.Fold(0);
        buffer.Edit(0, 13, 14, "");
        folds.Update(buffer, "//");

        Assert.That(folds.Folded, Is.Empty);
    }
}

using NUnit.Framework;
using Xanadu.Ui.Editing;

namespace Xanadu.Tests.UI;

/// <summary>The whole-line and word edits a code editor needs: indent, outdent, comment, word moves.</summary>
[TestFixture]
public sealed class TextBufferTests
{
    private static TextBuffer Buffer(string text)
    {
        var buffer = new TextBuffer();
        buffer.Load(text);
        return buffer;
    }

    [Test]
    public void TabWithASelectionIndentsItsLinesInsteadOfReplacingThem()
    {
        TextBuffer buffer = Buffer("a\n\nb\nc");
        buffer.Place(0, 0);
        buffer.Place(2, 1, extend: true);
        buffer.IndentLines();

        // The blank line stays blank; the selection still covers the same text.
        Assert.That(buffer.Text, Is.EqualTo("    a\n\n    b\nc"));
        Assert.That(buffer.SelectedText, Is.EqualTo("    a\n\n    b"));
    }

    [Test]
    public void ASelectionEndingAtALineStartLeavesThatLineOut()
    {
        TextBuffer buffer = Buffer("a\nb");
        buffer.Place(0, 0);
        buffer.Place(1, 0, extend: true);
        buffer.IndentLines();
        Assert.That(buffer.Text, Is.EqualTo("    a\nb"));
    }

    [Test]
    public void ShiftTabOutdentsByAStepOrToTheNextStep()
    {
        TextBuffer buffer = Buffer("        a\n      b\n c\nd");
        buffer.Place(0, 9);
        buffer.Place(3, 1, extend: true);
        buffer.OutdentLines();
        Assert.That(buffer.Text, Is.EqualTo("    a\n    b\nc\nd"));

        // With nothing selected, the caret's line.
        buffer.Place(0, 5);
        buffer.OutdentLines();
        Assert.That(buffer.Lines[0], Is.EqualTo("a"));
        Assert.That(buffer.Column, Is.EqualTo(1));
    }

    [Test]
    public void CtrlSlashCommentsAtTheSharedIndentAndBackOut()
    {
        TextBuffer buffer = Buffer("    if (x)\n        y();\n\n    z();");
        buffer.Place(0, 0);
        buffer.Place(3, 8, extend: true);
        buffer.ToggleComment("//");
        Assert.That(buffer.Text, Is.EqualTo("    // if (x)\n    //     y();\n\n    // z();"));

        buffer.ToggleComment("//");
        Assert.That(buffer.Text, Is.EqualTo("    if (x)\n        y();\n\n    z();"));
    }

    [Test]
    public void ALineWithoutTheMarkerMakesTheWholeSelectionAComment()
    {
        TextBuffer buffer = Buffer("// a\nb");
        buffer.Place(0, 0);
        buffer.Place(1, 1, extend: true);
        buffer.ToggleComment("//");
        Assert.That(buffer.Text, Is.EqualTo("// // a\n// b"));
    }

    [Test]
    public void CommentingKeepsTheCaretOnItsText()
    {
        TextBuffer buffer = Buffer("    abc");
        buffer.Place(0, 6);
        buffer.ToggleComment("//");
        Assert.That(buffer.Column, Is.EqualTo(9));
        buffer.ToggleComment("//");
        Assert.That(buffer.Column, Is.EqualTo(6));
    }

    [Test]
    public void WordMovesSkipSpacesThenAWordOrASymbolRun()
    {
        TextBuffer buffer = Buffer("col.paint(circle)  += 1;");
        buffer.MoveWord(1);
        Assert.That(buffer.Column, Is.EqualTo(3));
        buffer.MoveWord(1);
        Assert.That(buffer.Column, Is.EqualTo(4));
        buffer.Place(0, 17);
        buffer.MoveWord(1);
        Assert.That(buffer.Column, Is.EqualTo(21), "spaces, then +=");
        buffer.MoveWord(-1);
        Assert.That(buffer.Column, Is.EqualTo(19));
    }

    [Test]
    public void WordMovesCrossLineEnds()
    {
        TextBuffer buffer = Buffer("ab\ncd");
        buffer.Place(1, 0);
        buffer.MoveWord(-1);
        Assert.That((buffer.Line, buffer.Column), Is.EqualTo((0, 2)));
        buffer.MoveWord(1);
        Assert.That((buffer.Line, buffer.Column), Is.EqualTo((1, 0)));
    }

    [Test]
    public void CtrlBackspaceAndDeleteTakeAWord()
    {
        TextBuffer buffer = Buffer("float speed = 1;");
        buffer.Place(0, 11);
        buffer.DeleteWord(-1);
        Assert.That(buffer.Text, Is.EqualTo("float  = 1;"));
        buffer.DeleteWord(1);
        Assert.That(buffer.Text, Is.EqualTo("float  1;"));
    }
}

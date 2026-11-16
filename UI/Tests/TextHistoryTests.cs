using NUnit.Framework;
using Xanadu.Ui.Editing;

namespace Xanadu.Ui.Tests;

/// <summary>An owner holding the history while it changes the text over many frames (dragging a point): one step.</summary>
[TestFixture]
public sealed class TextHistoryTests
{
    [Test]
    public void ChangesWhileHeldAreOneStepEvenThroughPausesAndCaretMoves()
    {
        var buffer = new TextBuffer();
        buffer.Load("float2(0, 0)");
        var history = new TextHistory(buffer);

        history.Hold();
        for (int x = 1; x <= 5; x++)
        {
            buffer.Edit(0, 0, buffer.Lines[0].Length, $"float2({x}, 0)");
            // Frames that are not plain typing, a long pause and a caret move: none of them ends the step.
            history.After(typing: false, 0.016f);
            history.After(typing: false, 2f);
            buffer.Place(0, x);
            history.After(typing: false, 0.016f);
        }

        history.LetGo();
        history.After(typing: true, 0.016f);

        Assert.That(history.Undo(), Is.True);
        Assert.That(buffer.Text, Is.EqualTo("float2(0, 0)"));
        Assert.That(history.Undo(), Is.False);
        Assert.That(history.Redo(), Is.True);
        Assert.That(buffer.Text, Is.EqualTo("float2(5, 0)"));
    }

    [Test]
    public void AChangeTheFrameItIsLetGoIsInTheStepAndTypingAfterIsItsOwn()
    {
        var buffer = new TextBuffer();
        buffer.Load("a");
        var history = new TextHistory(buffer);

        history.Hold();
        buffer.Edit(0, 0, 1, "b");
        history.After(typing: false, 0.016f);
        buffer.Edit(0, 0, 1, "c");
        history.LetGo();
        history.After(typing: false, 0.016f);

        buffer.Place(0, 1);
        buffer.Insert("d");
        history.After(typing: true, 0.016f);
        history.After(typing: true, 2f);

        Assert.That(history.Undo(), Is.True);
        Assert.That(buffer.Text, Is.EqualTo("c"));
        Assert.That(history.Undo(), Is.True);
        Assert.That(buffer.Text, Is.EqualTo("a"));
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using Xanadu.History;

namespace Xanadu.Tests.History;

[TestFixture]
public sealed class UndoHistoryTests
{
    private int _state;
    private List<int> _applied = null!;
    private UndoHistory<int> _history = null!;

    [SetUp]
    public void SetUp()
    {
        _state = 0;
        _applied = new List<int>();
        _history = new UndoHistory<int>(
            (before, after) => before == after,
            value =>
            {
                _state = value;
                _applied.Add(value);
            });
    }

    private void Edit(int to)
    {
        int before = _state;
        _state = to;
        _history.Record(before, to);
    }

    [Test]
    public void UndoRestoresTheStateBeforeAndRedoTheStateAfter()
    {
        Edit(5);

        Assert.That(_history.Undo(), Is.True);
        Assert.That(_state, Is.EqualTo(0));
        Assert.That(_history.Redo(), Is.True);
        Assert.That(_state, Is.EqualTo(5));
    }

    [Test]
    public void UndoWalksBackThroughEditsInOrder()
    {
        Edit(1);
        Edit(2);
        Edit(3);

        _history.Undo();
        _history.Undo();

        Assert.That(_applied, Is.EqualTo(new[] { 2, 1 }));
        Assert.That(_history.CanRedo, Is.True);
    }

    [Test]
    public void NothingToUndoOrRedoReturnsFalseAndAppliesNothing()
    {
        Assert.That(_history.Undo(), Is.False);
        Assert.That(_history.Redo(), Is.False);
        Assert.That(_applied, Is.Empty);
    }

    [Test]
    public void EditsThatChangedNothingAreNotRecorded()
    {
        Edit(0);

        Assert.That(_history.CanUndo, Is.False);
    }

    [Test]
    public void RecordingANewEditDropsTheRedoStack()
    {
        Edit(1);
        _history.Undo();

        Edit(9);

        Assert.That(_history.CanRedo, Is.False);
        Assert.That(_history.Redo(), Is.False);
    }

    [Test]
    public void ClearForgetsEverything()
    {
        Edit(1);
        _history.Undo();

        _history.Clear();

        Assert.That(_history.CanUndo, Is.False);
        Assert.That(_history.CanRedo, Is.False);
    }

    [Test]
    public void OnlyTheNewestEntriesAreKept()
    {
        var limited = new UndoHistory<int>((a, b) => a == b, value => _state = value, maxEntries: 3);
        for (int i = 0; i < 6; i++)
            limited.Record(i, i + 1);

        Assert.That(limited.UndoCount, Is.EqualTo(3));
        int undone = 0;
        while (limited.Undo())
            undone++;
        Assert.That(undone, Is.EqualTo(3));
        Assert.That(_state, Is.EqualTo(3));
    }
}

using NUnit.Framework;
using Xanadu.Systems.Playfields;

namespace Xanadu.Tests.Playfields;

[TestFixture]
public sealed class NotePopTests
{
    [Test]
    public void PopStartsAtTheNoteAndSwellingOut()
    {
        Assert.That(
            PlayfieldRenderer.NotePopRadius(10f, 0f),
            Is.EqualTo(10f).Within(0.001f));
        Assert.That(
            PlayfieldRenderer.NotePopRadius(10f, 1f),
            Is.EqualTo(15f).Within(0.001f));
        Assert.That(
            PlayfieldRenderer.NotePopRadius(10f, 0.5f),
            Is.GreaterThan(10f).And.LessThan(15f));
    }

    [Test]
    public void PopNeverShrinksBelowTheNote()
    {
        for (int i = 0; i <= 10; i++)
        {
            Assert.That(
                PlayfieldRenderer.NotePopRadius(10f, i / 10f),
                Is.GreaterThanOrEqualTo(10f));
        }
    }

    [Test]
    public void PopFadesOutOverItsLife()
    {
        Assert.That(PlayfieldRenderer.NotePopAlpha(0f), Is.EqualTo(1f));
        Assert.That(PlayfieldRenderer.NotePopAlpha(1f), Is.EqualTo(0f));
        Assert.That(PlayfieldRenderer.NotePopAlpha(0.5f), Is.EqualTo(0.5f).Within(0.001f));
    }
}

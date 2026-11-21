using NUnit.Framework;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Playfields;

namespace Xanadu.Tests.Playfields;

[TestFixture]
public sealed class NoteFadeTests
{
    [Test]
    public void FadesInAndOutAroundNoteTime()
    {
        var note = new Note(1_000, Vector2.Zero);

        Assert.That(NoteFade.Alpha(note, 400), Is.EqualTo(0f));
        Assert.That(NoteFade.Alpha(note, 700), Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(NoteFade.Alpha(note, 1_000), Is.EqualTo(1f));
        Assert.That(NoteFade.Alpha(note, 1_150), Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(NoteFade.Alpha(note, 1_300), Is.EqualTo(0f));
    }

}

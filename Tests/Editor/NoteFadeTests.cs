using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Systems.Editor;

namespace Writer.Tests.Editor;

[TestFixture]
public sealed class NoteFadeTests
{
    [Test]
    public void FadesInAndOutAroundNoteTime()
    {
        var note = new Note(1_000, Vector2.Zero, fadeInMs: 600, fadeOutMs: 600);

        Assert.That(NoteFade.Alpha(note, 400), Is.EqualTo(0f));
        Assert.That(NoteFade.Alpha(note, 700), Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(NoteFade.Alpha(note, 1_000), Is.EqualTo(1f));
        Assert.That(NoteFade.Alpha(note, 1_300), Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(NoteFade.Alpha(note, 1_600), Is.EqualTo(0f));
    }
}

using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Systems.Playfields;

namespace Writer.Tests.Playfields;

[TestFixture]
public sealed class NoteFadeTests
{
    [Test]
    public void FadesInAndOutAroundNoteTime()
    {
        var note = new Note(1_000, Vector2.Zero);

        Assert.That(NoteFade.Alpha(note, 700), Is.EqualTo(0f));
        Assert.That(NoteFade.Alpha(note, 850), Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(NoteFade.Alpha(note, 1_000), Is.EqualTo(1f));
        Assert.That(NoteFade.Alpha(note, 1_150), Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(NoteFade.Alpha(note, 1_300), Is.EqualTo(0f));
    }

    [Test]
    public void HitAndMovementNotesShareFadeAndApproach()
    {
        var hit = new Note(1_000, Vector2.Zero);
        var movement = new Note(
            1_000,
            Vector2.Zero,
            kind: NoteKind.Movement);

        foreach (long time in new long[] { 700, 850, 1_000, 1_150, 1_300 })
        {
            Assert.That(
                NoteFade.Alpha(movement, time),
                Is.EqualTo(NoteFade.Alpha(hit, time)));
            Assert.That(
                NoteFade.Approach(movement.TimeMs, time),
                Is.EqualTo(NoteFade.Approach(hit.TimeMs, time)));
        }
    }
}

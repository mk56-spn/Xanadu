using NUnit.Framework;
using Writer.Ecs;
using Writer.Systems.Editor;

namespace Writer.Tests.Editor;

[TestFixture]
public sealed class EditorScrollTests
{
    [Test]
    public void WheelStepsThroughTheSongAndClamps()
    {
        var playhead = new Playhead(1_000, 120, 0, false, durationMs: 5_000);

        Assert.That(
            EditorScroll.Target(playhead, 1),
            Is.EqualTo(1_500));
        Assert.That(
            EditorScroll.Target(playhead, -1),
            Is.EqualTo(500));
        Assert.That(
            EditorScroll.Target(playhead, 1, fast: true),
            Is.EqualTo(3_000));
    }

    [Test]
    public void WheelDoesNotSeekPastEitherEnd()
    {
        var playhead = new Playhead(100, 120, 0, false, durationMs: 5_000);

        Assert.That(EditorScroll.Target(playhead, -1), Is.Zero);
        Assert.That(
            EditorScroll.Target(playhead with { TimeMs = 4_800 }, 1),
            Is.EqualTo(5_000));
    }
}

using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Editor;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class EditorTimingTests
{
    [Test]
    public void SnapsToQuarterBeat()
    {
        var playhead = new Playhead(0, 120, 0, false, snapDivision: 4);

        Assert.That(EditorTiming.Snap(1_300, playhead), Is.EqualTo(1_500));
        Assert.That(EditorTiming.Snap(1_120, playhead), Is.EqualTo(1_000));
    }

    [Test]
    public void SnapStepTracksBpmAndDivision()
    {
        var playhead = new Playhead(0, 120, 0, false, snapDivision: 8);

        Assert.That(EditorTiming.StepMs(playhead), Is.EqualTo(250));
    }

    [Test]
    public void OffsetIsAppliedBeforeSnapping()
    {
        var playhead = new Playhead(0, 120, 250, false, snapDivision: 8);

        Assert.That(EditorTiming.Snap(1_300, playhead), Is.EqualTo(1_250));
    }
}

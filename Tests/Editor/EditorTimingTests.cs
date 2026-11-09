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
        // 1/4 at 120 BPM: a beat is 500 ms, so the grid is every 125 ms.
        var playhead = new Playhead(0, 120, 0, false, snapDivision: 4);

        Assert.That(EditorTiming.Snap(1_300, playhead), Is.EqualTo(1_250));
        Assert.That(EditorTiming.Snap(1_190, playhead), Is.EqualTo(1_250));
        Assert.That(EditorTiming.Snap(1_060, playhead), Is.EqualTo(1_000));
    }

    [Test]
    public void TripletsSplitTheBeatInThree()
    {
        // 1/3 at 100 BPM: a beat is 600 ms, so the grid is every 200 ms.
        var playhead = new Playhead(0, 100, 0, false, snapDivision: 3);

        Assert.That(EditorTiming.StepMs(playhead), Is.EqualTo(200).Within(0.001));
        Assert.That(EditorTiming.Snap(1_290, playhead), Is.EqualTo(1_200));
        Assert.That(EditorTiming.Snap(1_310, playhead), Is.EqualTo(1_400));
    }

    [Test]
    public void OneOverOneSnapsToWholeBeats()
    {
        var playhead = new Playhead(0, 120, 0, false, snapDivision: 1);

        Assert.That(EditorTiming.Snap(1_300, playhead), Is.EqualTo(1_500));
        Assert.That(EditorTiming.StepMs(playhead), Is.EqualTo(500));
    }

    [Test]
    public void SnapStepTracksBpmAndDivision()
    {
        var playhead = new Playhead(0, 120, 0, false, snapDivision: 2);

        Assert.That(EditorTiming.StepMs(playhead), Is.EqualTo(250));
    }

    [Test]
    public void OffsetIsAppliedBeforeSnapping()
    {
        var playhead = new Playhead(0, 120, 250, false, snapDivision: 2);

        Assert.That(EditorTiming.Snap(1_300, playhead), Is.EqualTo(1_250));
    }
}

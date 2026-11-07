using System.Linq;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Systems.Editor;

namespace Xanadu.Tests.Timing;

/// <summary>Song timing with BPM changes: the map every beat-based feature asks.</summary>
[TestFixture]
public sealed class TempoMapTests
{
    /// <summary>120 BPM from 1 s, 60 BPM from beat 8 (5 s), 3 beats to a bar there.</summary>
    private static TempoMap TwoSections() => TempoMap.From(
        new TempoPoint(1_000, 120),
        new[] { new TempoPoint(5_000, 60, 3) });

    [Test]
    public void OneTempoMatchesTheOldGrid()
    {
        TempoMap map = TempoMap.Single(120, 250);

        Assert.That(map.BeatAt(250), Is.EqualTo(0));
        Assert.That(map.BeatAt(1_250), Is.EqualTo(2));
        Assert.That(map.TimeAt(-1), Is.EqualTo(-250), "the first tempo runs back before the offset");
        Assert.That(map.Snap(1_300, 0.5), Is.EqualTo(1_250));
    }

    [Test]
    public void BeatsAndTimesFollowAChange()
    {
        TempoMap map = TwoSections();

        Assert.That(map.BeatAt(5_000), Is.EqualTo(8));
        Assert.That(map.BeatAt(6_000), Is.EqualTo(9), "one beat a second after the change");
        Assert.That(map.TimeAt(10), Is.EqualTo(7_000));
        Assert.That(map.At(6_000).Bpm, Is.EqualTo(60));
        Assert.That(map.Snap(6_400, 1), Is.EqualTo(6_000), "snapping uses the slower beat after the change");
    }

    [Test]
    public void AChangeBetweenBeatsMovesToTheNextBeat()
    {
        TempoMap map = TempoMap.From(new TempoPoint(0, 120), new[] { new TempoPoint(1_100, 90) });

        Assert.That(map.Points[1].TimeMs, Is.EqualTo(1_500));
    }

    [Test]
    public void BarsRestartAtEachSection()
    {
        TempoMap map = TwoSections();

        double[] bars = map.Ticks(1_000, 9_000, 1).Where(tick => tick.OnBar).Select(tick => tick.TimeMs).ToArray();

        Assert.That(bars, Is.EqualTo(new double[] { 1_000, 3_000, 5_000, 8_000 }), "4/4 bars, then 3-beat bars from the change");
    }

    [Test]
    public void TheEditorSnapsAndStepsOnTheMap()
    {
        var playhead = new Playhead(0, 120, 0, false, snapDivision: 4) { Tempo = TwoSections() };

        Assert.That(EditorTiming.Snap(6_400, playhead), Is.EqualTo(6_000));
        Assert.That(EditorTiming.Step(4_500, playhead, 1), Is.EqualTo(5_000), "the last 120 BPM beat to the first 60 BPM one");
        Assert.That(EditorTiming.Step(5_000, playhead, 1), Is.EqualTo(6_000));
    }
}

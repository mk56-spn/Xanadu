using NUnit.Framework;
using Xanadu.Levels;
using Xanadu.Systems.Characters;
using Rules = Xanadu.Systems.Characters.CharacterPresenceRules;

namespace Xanadu.Tests.Characters;

public sealed class CharacterPresenceRulesTests
{
    // 120 BPM: a beat is 500 ms; the first beat is at 0.
    private static readonly TempoMap Tempo = TempoMap.Single(120, 0);

    [Test]
    public void IsAwayBetweenNotesMoreThanASecondApart()
    {
        long[] away = Rules.AwayWindows([(2_000, 2_000), (6_000, 6_000)]);
        Assert.That(away, Is.EqualTo(new[] { long.MinValue, 1_000, 3_000, 5_000, 7_000, long.MaxValue }));
    }

    [Test]
    public void AHoldKeepsHerUntilItsEnd()
    {
        long[] away = Rules.AwayWindows([(2_000, 4_000), (8_000, 8_000)]);
        Assert.That(away[2], Is.EqualTo(5_000));
    }

    [Test]
    public void SkipsAShortRest()
    {
        long[] away = Rules.AwayWindows([(2_000, 2_000), (4_200, 4_200)]);
        Assert.That(away, Is.EqualTo(new[] { long.MinValue, 1_000, 5_200, long.MaxValue }));
    }

    [Test]
    public void IsOnStageNearANote()
    {
        long[] away = Rules.AwayWindows([(2_000, 2_000), (6_000, 6_000)]);
        Assert.That(Rules.At(2_900, away, Tempo), Is.EqualTo(Rules.Look.Present));
    }

    [Test]
    public void FlashesWhiteOnTheWayOut()
    {
        long[] away = Rules.AwayWindows([(2_000, 2_000), (6_000, 6_000)]);
        Rules.Look look = Rules.At(3_000 + Rules.FlashMs - 1, away, Tempo);
        Assert.That(look.Shown, Is.True);
        Assert.That(look.Light, Is.EqualTo(Rules.FlashLight).Within(0.05));
    }

    [Test]
    public void TheRhombusPulsesOnTheBeat()
    {
        long[] away = Rules.AwayWindows([(2_000, 2_000), (6_000, 6_000)]);
        Rules.Look onBeat = Rules.At(4_000, away, Tempo);
        Rules.Look offBeat = Rules.At(4_250, away, Tempo);
        Assert.That(onBeat.Shown, Is.False);
        Assert.That(onBeat.Rhombus, Is.EqualTo(1f).Within(1e-4));
        Assert.That(onBeat.Pulse, Is.EqualTo(1f).Within(1e-4));
        Assert.That(offBeat.Pulse, Is.LessThan(0.1f));
    }

    [Test]
    public void ComesBackASecondBeforeTheNextNote()
    {
        long[] away = Rules.AwayWindows([(2_000, 2_000), (6_000, 6_000)]);
        Assert.That(Rules.At(4_999, away, Tempo).Shown, Is.False);
        Rules.Look back = Rules.At(5_000, away, Tempo);
        Assert.That(back.Shown, Is.True);
        Assert.That(back.Light, Is.EqualTo(Rules.FlashLight).Within(1e-4));
    }

    [Test]
    public void NoNotesMeansAlwaysOnStage()
    {
        Assert.That(Rules.At(10_000, Rules.AwayWindows([]), Tempo), Is.EqualTo(Rules.Look.Present));
    }
}

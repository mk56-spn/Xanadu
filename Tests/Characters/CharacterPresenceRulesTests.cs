using NUnit.Framework;
using Xanadu.Systems.Characters;

namespace Xanadu.Tests.Characters;

public sealed class CharacterPresenceRulesTests
{
    // 120 BPM: a beat is 500 ms; the first beat is at 0.
    private const int Bpm = 120;

    [Test]
    public void StartsFadingOnTheHitBeat()
    {
        CharacterPresenceRules.Look look = CharacterPresenceRules.At(125, 4_125, Bpm, 0);
        Assert.That(look.BodyShade, Is.LessThan(1f), "the fade must not wait a beat after the hit");
    }

    [Test]
    public void EasesIntoAndOutOfTheFade()
    {
        // A quarter and three quarters through the fade; eased, that is 1/16 and 15/16 of the way down.
        CharacterPresenceRules.Look quarter = CharacterPresenceRules.At(125, 4_125, Bpm, 0);
        CharacterPresenceRules.Look threeQuarters = CharacterPresenceRules.At(375, 4_375, Bpm, 0);
        float drop = 1f - CharacterPresenceRules.FadedShade;
        Assert.That(quarter.BodyShade, Is.EqualTo(1f - drop * 0.0625f).Within(1e-4));
        Assert.That(threeQuarters.BodyShade, Is.EqualTo(1f - drop * 0.9375f).Within(1e-4));
    }

    [Test]
    public void DimsToTheFadedShadeOnceTheFadeIsDone()
    {
        CharacterPresenceRules.Look look = CharacterPresenceRules.At(2_000, 4_250, Bpm, 0);
        Assert.That(look.BodyShade, Is.EqualTo(CharacterPresenceRules.FadedShade).Within(1e-4));
    }

    [Test]
    public void TheLeftHandTakesEvenBeatsAndTheRightOdd()
    {
        CharacterPresenceRules.Look even = CharacterPresenceRules.At(2_000, 4_000, Bpm, 0);
        Assert.That(even.LeftLight, Is.EqualTo(CharacterPresenceRules.PulseLight).Within(1e-4));
        Assert.That(even.RightLight, Is.EqualTo(1f));

        CharacterPresenceRules.Look odd = CharacterPresenceRules.At(2_000, 4_500, Bpm, 0);
        Assert.That(odd.RightLight, Is.EqualTo(CharacterPresenceRules.PulseLight).Within(1e-4));
        Assert.That(odd.LeftLight, Is.EqualTo(1f));
    }

    [Test]
    public void BeatsCountFromTheSongOffset()
    {
        // 4 100 ms is beat 8 of a song whose first beat is at 100.
        CharacterPresenceRules.Look look = CharacterPresenceRules.At(2_000, 4_100, Bpm, 100);
        Assert.That(look.LeftLight, Is.EqualTo(CharacterPresenceRules.PulseLight).Within(1e-4));
    }

    [Test]
    public void ASongWithNoTempoKeepsHerWhole()
    {
        Assert.That(CharacterPresenceRules.At(10_000, 10_000, 0, 0), Is.EqualTo(CharacterPresenceRules.Look.Present));
    }
}

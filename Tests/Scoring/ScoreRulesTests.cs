using NUnit.Framework;
using Xanadu.Scoring;

namespace Xanadu.Tests.Scoring;

[TestFixture]
public sealed class ScoreRulesTests
{
    [TestCase(0L, Judgement.FlawlessP)]
    [TestCase(8L, Judgement.FlawlessP)]
    [TestCase(9L, Judgement.Flawless)]
    [TestCase(16L, Judgement.Flawless)]
    [TestCase(17L, Judgement.Clean)]
    [TestCase(27L, Judgement.Clean)]
    [TestCase(28L, Judgement.Fair)]
    [TestCase(40L, Judgement.Fair)]
    [TestCase(41L, Judgement.Deficient)]
    [TestCase(55L, Judgement.Deficient)]
    [TestCase(56L, Judgement.Terrible)]
    [TestCase(80L, Judgement.Terrible)]
    [TestCase(81L, Judgement.Miss)]
    [TestCase(150L, Judgement.Miss)]
    [TestCase(151L, Judgement.Miss)]
    public void JudgeUsesTheSuppliedDeviationWindows(
        long offsetMs,
        Judgement expected)
    {
        Assert.That(
            ScoreRules.Judge(offsetMs),
            Is.EqualTo(expected));
    }

    [Test]
    public void RankUsesTheSuppliedAccuracyThresholds()
    {
        Assert.That(ScoreRules.RankFor(1.0), Is.EqualTo("SSS"));
        Assert.That(ScoreRules.RankFor(0.999), Is.EqualTo("SSS"));
        Assert.That(ScoreRules.RankFor(0.99), Is.EqualTo("SS"));
        Assert.That(ScoreRules.RankFor(0.98), Is.EqualTo("S"));
        Assert.That(ScoreRules.RankFor(0.95), Is.EqualTo("A"));
        Assert.That(ScoreRules.RankFor(0.93), Is.EqualTo("B"));
        Assert.That(ScoreRules.RankFor(0.90), Is.EqualTo("C"));
        Assert.That(ScoreRules.RankFor(0.8999), Is.EqualTo("FAIL"));
    }

    [Test]
    public void AccuracyWeightsLineUpWithTheRankTiers()
    {
        Assert.That(ScoreRules.AccuracyFor(Judgement.FlawlessP), Is.EqualTo(1.0));
        Assert.That(ScoreRules.AccuracyFor(Judgement.Flawless), Is.EqualTo(0.99));
        Assert.That(ScoreRules.AccuracyFor(Judgement.Clean), Is.EqualTo(0.98));
        Assert.That(ScoreRules.AccuracyFor(Judgement.Fair), Is.EqualTo(0.95));
        Assert.That(ScoreRules.AccuracyFor(Judgement.Deficient), Is.EqualTo(0.93));
        Assert.That(ScoreRules.AccuracyFor(Judgement.Terrible), Is.EqualTo(0.90));
        Assert.That(ScoreRules.AccuracyFor(Judgement.Miss), Is.EqualTo(0.0));
    }
}

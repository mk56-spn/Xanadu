using NUnit.Framework;
using Xanadu.Ecs;

namespace Xanadu.Tests.UI;

[TestFixture]
public sealed class UiTextMetricsTests
{
    [Test]
    public void LongerTextIsWiderAndBiggerTypeIsWider()
    {
        Assert.That(UiTextMetrics.Width("ABCDEF", 20), Is.GreaterThan(UiTextMetrics.Width("ABC", 20)));
        Assert.That(UiTextMetrics.Width("ABC", 40), Is.GreaterThan(UiTextMetrics.Width("ABC", 20)));
    }

    [Test]
    public void BoldIsWiderAndNarrowLettersAreNarrow()
    {
        Assert.That(UiTextMetrics.Width("Level", 24, bold: true), Is.GreaterThan(UiTextMetrics.Width("Level", 24)));
        Assert.That(UiTextMetrics.Width("iiii", 24), Is.LessThan(UiTextMetrics.Width("WWWW", 24)));
    }

    [Test]
    public void NothingHasNoWidth() =>
        Assert.That(UiTextMetrics.Width("", 30), Is.Zero);
}

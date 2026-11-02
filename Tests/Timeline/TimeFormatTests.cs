using NUnit.Framework;
using Xanadu.Timeline;

namespace Xanadu.Tests.Timeline;

[TestFixture]
public sealed class TimeFormatTests
{
    [TestCase(0, "00:00.000")]
    [TestCase(61_234, "01:01.234")]
    [TestCase(6_000_000, "100:00.000")]
    [TestCase(-5, "00:00.000")]
    public void ClockWritesMinutesSecondsAndMilliseconds(long milliseconds, string expected) =>
        Assert.That(TimeFormat.Clock(milliseconds), Is.EqualTo(expected));
}

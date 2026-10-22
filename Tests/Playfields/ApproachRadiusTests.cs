using NUnit.Framework;
using Writer.Systems.Playfields;

namespace Writer.Tests.Playfields;

[TestFixture]
public sealed class ApproachRadiusTests
{
    [Test]
    public void RingIsThreeTimesTheOriginalApproachSize()
    {
        Assert.That(
            PlayfieldRenderer.ApproachRadius(10f, 1f),
            Is.EqualTo(66f).Within(0.001f));
        Assert.That(
            PlayfieldRenderer.ApproachRadius(10f, 0f),
            Is.EqualTo(30f).Within(0.001f));
    }
}

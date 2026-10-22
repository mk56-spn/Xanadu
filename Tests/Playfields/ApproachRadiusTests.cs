using NUnit.Framework;
using Writer.Systems.Playfields;

namespace Writer.Tests.Playfields;

[TestFixture]
public sealed class ApproachRadiusTests
{
    [Test]
    public void RingClosesExactlyOntoTheNote()
    {
        Assert.That(
            PlayfieldRenderer.ApproachRadius(10f, 1f),
            Is.EqualTo(30f).Within(0.001f));
        Assert.That(
            PlayfieldRenderer.ApproachRadius(10f, 0.5f),
            Is.EqualTo(20f).Within(0.001f));
        Assert.That(
            PlayfieldRenderer.ApproachRadius(10f, 0f),
            Is.EqualTo(10f).Within(0.001f));
    }
}

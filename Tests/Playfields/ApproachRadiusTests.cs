using NUnit.Framework;
using Xanadu.Systems.Playfields;

namespace Xanadu.Tests.Playfields;

[TestFixture]
public sealed class ApproachRadiusTests
{
    [Test]
    public void RingClosesExactlyOntoTheNote()
    {
        Assert.That(
            PlayfieldRenderer.ApproachRadius(10f, 1f),
            Is.EqualTo(90f).Within(0.001f));
        Assert.That(
            PlayfieldRenderer.ApproachRadius(10f, 0.5f),
            Is.EqualTo(50f).Within(0.001f));
        Assert.That(
            PlayfieldRenderer.ApproachRadius(10f, 0f),
            Is.EqualTo(10f).Within(0.001f));
    }

    [Test]
    public void ApproachWindowIsIndependentOfNoteFade()
    {
        Assert.That(NoteFade.ApproachWindowMs, Is.EqualTo(600f));
    }
}

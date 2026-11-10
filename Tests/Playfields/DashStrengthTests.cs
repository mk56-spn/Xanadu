using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Playfields;

namespace Xanadu.Tests.Playfields;

[TestFixture]
public sealed class DashStrengthTests
{
    [TestCase(DashStrength.Single, 1, "")]
    [TestCase(DashStrength.Double, 2, "×2")]
    [TestCase(DashStrength.Triple, 3, "×3")]
    public void StrengthLevelAndBadgeFollowTheStrength(
        DashStrength strength,
        int level,
        string badge)
    {
        Assert.That(PlayfieldRenderer.DashStrengthLevel(strength), Is.EqualTo(level));
        Assert.That(PlayfieldRenderer.DashStrengthLabel(strength), Is.EqualTo(badge));
    }

    [TestCase(DashStrength.Single, 250f)]
    [TestCase(DashStrength.Double, 375f)]
    [TestCase(DashStrength.Triple, 500f)]
    public void DashTargetMovesTheDistanceForItsStrength(
        DashStrength strength,
        float expectedX)
    {
        Vector2 target = PlayerMovementSystem.DashTarget(
            Vector2.Zero,
            DashDirection.Right,
            strength);

        Assert.That(target.X, Is.EqualTo(expectedX));
        Assert.That(target.Y, Is.EqualTo(0f));
    }
}

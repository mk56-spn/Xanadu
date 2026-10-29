using Godot;
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
    public void StrengthLevelAndBadgeMatchTheMultiplier(
        DashStrength strength,
        int level,
        string badge)
    {
        Assert.That(PlayfieldRenderer.DashStrengthLevel(strength), Is.EqualTo(level));
        Assert.That(PlayfieldRenderer.DashStrengthLabel(strength), Is.EqualTo(badge));
        Assert.That(PlayerMovementSystem.DashStrengthMultiplier(strength),
            Is.EqualTo(level));
    }

    [TestCase(DashStrength.Single, 150f)]
    [TestCase(DashStrength.Double, 300f)]
    [TestCase(DashStrength.Triple, 450f)]
    public void DashTargetMovesOneTwoOrThreeLengths(
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

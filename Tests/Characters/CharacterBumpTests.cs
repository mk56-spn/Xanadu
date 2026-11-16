using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Ecs.Components;
using Xanadu.Systems.Characters;

namespace Xanadu.Tests.Characters;

/// <summary>The flinch a bump into a block shows: a jolt that springs back, away from the block.</summary>
[TestFixture]
public sealed class CharacterBumpTests
{
    [Test]
    public void TheFlinchJoltsThenDiesAway()
    {
        Assert.That(CharacterBumpSystem.Envelope(0.03f), Is.GreaterThan(0.6f));
        Assert.That(CharacterBumpSystem.Envelope(CharacterBumpSystem.Seconds), Is.EqualTo(0f));
        Assert.That(CharacterBumpSystem.Envelope(-0.1f), Is.EqualTo(0f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void HerUpperBodyLeansAwayFromAWallAhead(bool faceLeft)
    {
        XafPackage package = Rig();
        var pose = new XafPose(1, 0);
        // A wall ahead of her pushes back the other way.
        var bump = new CharacterBump { Normal = new Vector2(faceLeft ? 1f : -1f, 0f), Strength = 1f, Age = 0.03f };

        CharacterBumpSystem.ApplyFlinch(package, pose, bump, faceLeft);

        // Rig space faces right: a negative turn tips the top backwards.
        Assert.That(pose.Bones[0].Rotation, Is.LessThan(0f));
    }

    [Test]
    public void NoFlinchLeavesThePoseAlone()
    {
        XafPackage package = Rig();
        var pose = new XafPose(1, 0);
        var bump = new CharacterBump
            { Normal = new Vector2(-1f, 0f), Strength = 1f, Age = CharacterBumpSystem.Seconds };

        CharacterBumpSystem.ApplyFlinch(package, pose, bump, false);

        Assert.That(pose.Bones[0].Rotation, Is.EqualTo(0f));
    }

    private static XafPackage Rig()
    {
        var package = new XafPackage();
        package.Skeleton.Bones.Add(new XafBone { Id = "spine" });
        return package;
    }
}

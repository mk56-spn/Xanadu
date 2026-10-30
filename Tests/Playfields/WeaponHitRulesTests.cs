using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Playfields;

namespace Xanadu.Tests.Playfields;

[TestFixture]
public sealed class WeaponHitRulesTests
{
    [Test]
    public void SpearReachesForwardButNotBehindOrTooFar()
    {
        var player = new Vector2(500, 500);

        Assert.That(
            WeaponHitRules.Contains(
                WeaponKind.Spear, player, facesLeft: false,
                new Vector2(760, 500), scale: 1f),
            Is.True);
        Assert.That(
            WeaponHitRules.Contains(
                WeaponKind.Spear, player, facesLeft: false,
                new Vector2(240, 500), scale: 1f),
            Is.False);
        Assert.That(
            WeaponHitRules.Contains(
                WeaponKind.Spear, player, facesLeft: false,
                new Vector2(790, 500), scale: 1f),
            Is.False);
        Assert.That(
            WeaponHitRules.Contains(
                WeaponKind.Spear, player, facesLeft: false,
                new Vector2(600, 520), scale: 1f),
            Is.False);
    }

    [Test]
    public void SpearFlipsWithFacing()
    {
        Assert.That(
            WeaponHitRules.Contains(
                WeaponKind.Spear, new Vector2(500, 500), facesLeft: true,
                new Vector2(300, 500), scale: 1f),
            Is.True);
        Assert.That(
            WeaponHitRules.Contains(
                WeaponKind.Spear, new Vector2(500, 500), facesLeft: true,
                new Vector2(700, 500), scale: 1f),
            Is.False);
    }

    [Test]
    public void AxeHitsTheForwardAnnulusOnly()
    {
        var player = new Vector2(500, 500);

        Assert.That(
            WeaponHitRules.Contains(
                WeaponKind.Axe, player, facesLeft: false,
                new Vector2(650, 500), scale: 1f),
            Is.True);
        Assert.That(
            WeaponHitRules.Contains(
                WeaponKind.Axe, player, facesLeft: false,
                new Vector2(530, 500), scale: 1f),
            Is.False);
        Assert.That(
            WeaponHitRules.Contains(
                WeaponKind.Axe, player, facesLeft: false,
                new Vector2(800, 500), scale: 1f),
            Is.False);
        Assert.That(
            WeaponHitRules.Contains(
                WeaponKind.Axe, player, facesLeft: false,
                new Vector2(350, 500), scale: 1f),
            Is.False);
    }

    [Test]
    public void AxeIsAnArcNotAWholeCircle()
    {
        Assert.That(
            WeaponHitRules.Contains(
                WeaponKind.Axe, new Vector2(500, 500), facesLeft: false,
                new Vector2(500, 350), scale: 1f),
            Is.True);
        Assert.That(
            WeaponHitRules.Contains(
                WeaponKind.Axe, new Vector2(500, 500), facesLeft: false,
                new Vector2(500, 650), scale: 1f),
            Is.True);
        Assert.That(
            WeaponHitRules.Contains(
                WeaponKind.Axe, new Vector2(500, 500), facesLeft: false,
                new Vector2(350, 500), scale: 1f),
            Is.False);
    }
}

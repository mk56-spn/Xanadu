using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Playfields;

namespace Xanadu.Tests.Playfields;

/// <summary>The combo counter swells on a hit, harder on a milestone, flashes on a break, and settles.</summary>
[TestFixture]
public sealed class ComboHudTests
{
    [Test]
    public void AHitSwellsTheNumberAndItSettlesBack()
    {
        var hud = new ComboHud(default, default);

        ComboHudSystem.Step(ref hud, 1, 0f);
        Assert.That(ComboHudSystem.FontSize(hud), Is.GreaterThan(ComboHudSystem.Size));

        for (int i = 0; i < 60; i++)
            ComboHudSystem.Step(ref hud, 1, 1f / 60f);
        Assert.That(ComboHudSystem.FontSize(hud), Is.EqualTo(ComboHudSystem.Size));
    }

    [Test]
    public void AMilestoneSwellsHarderThanAnOrdinaryHit()
    {
        var ordinary = new ComboHud(default, default) { Shown = 10 };
        var milestone = new ComboHud(default, default) { Shown = ComboHudSystem.Milestone - 1 };

        ComboHudSystem.Step(ref ordinary, 11, 0f);
        ComboHudSystem.Step(ref milestone, ComboHudSystem.Milestone, 0f);

        Assert.That(ComboHudSystem.FontSize(milestone), Is.GreaterThan(ComboHudSystem.FontSize(ordinary)));
    }

    [Test]
    public void ABreakFlashesButLosingASingleHitDoesNot()
    {
        var broken = new ComboHud(default, default) { Shown = 12 };
        var single = new ComboHud(default, default) { Shown = 1 };

        ComboHudSystem.Step(ref broken, 0, 0f);
        ComboHudSystem.Step(ref single, 0, 0f);

        Assert.That(broken.Break, Is.EqualTo(1f));
        Assert.That(single.Break, Is.EqualTo(0f));
    }
}

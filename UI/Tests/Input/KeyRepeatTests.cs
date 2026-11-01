using NUnit.Framework;
using Xanadu.Systems.Input;

namespace Xanadu.Tests.UI;

[TestFixture]
public sealed class KeyRepeatTests
{
    private const float Frame = 1f / 60f;

    private static int Hold(KeyRepeat repeat, float seconds, int direction = 1)
    {
        int steps = 0;
        for (float t = 0; t < seconds; t += Frame)
            steps += repeat.Update(direction, Frame);
        return steps;
    }

    [Test]
    public void ATapMovesOnce()
    {
        var repeat = new KeyRepeat();

        Assert.That(repeat.Update(1, Frame), Is.EqualTo(1));
        Assert.That(repeat.Update(0, Frame), Is.EqualTo(0));
        Assert.That(repeat.Update(0, Frame), Is.EqualTo(0));
    }

    [Test]
    public void NothingRepeatsBeforeThePause()
    {
        var repeat = new KeyRepeat(delay: 0.35f);

        Assert.That(Hold(repeat, 0.3f), Is.EqualTo(1), "only the first step");
    }

    [Test]
    public void AHoldKeepsGoingAfterThePause()
    {
        var repeat = new KeyRepeat(delay: 0.35f);

        Assert.That(Hold(repeat, 1f), Is.GreaterThan(3));
    }

    [Test]
    public void TheLongerItIsHeldTheFasterItGoes()
    {
        var repeat = new KeyRepeat();
        Hold(repeat, 0.36f);

        int early = Hold(repeat, 0.5f);
        Hold(repeat, 1.5f);
        int late = Hold(repeat, 0.5f);

        Assert.That(late, Is.GreaterThan(early));
    }

    [Test]
    public void ItDoesNotDependOnTheFrameRate()
    {
        var fast = new KeyRepeat();
        var slow = new KeyRepeat();
        int atFast = 0;
        for (int i = 0; i < 240; i++)
            atFast += fast.Update(1, 1f / 240f);
        int atSlow = 0;
        for (int i = 0; i < 30; i++)
            atSlow += slow.Update(1, 1f / 30f);

        Assert.That(atSlow, Is.EqualTo(atFast).Within(1));
    }

    [Test]
    public void ChangingDirectionStartsOverAtOnce()
    {
        var repeat = new KeyRepeat();
        Hold(repeat, 2f);

        Assert.That(repeat.Update(-1, Frame), Is.EqualTo(1));
        Assert.That(Hold(repeat, 0.3f, -1), Is.EqualTo(0), "the pause applies again");
    }

    [Test]
    public void AnIgnoredKeyDoesNothingUntilItIsLetGo()
    {
        var repeat = new KeyRepeat();
        repeat.Ignore(1);

        Assert.That(Hold(repeat, 2f), Is.EqualTo(0));
        Assert.That(repeat.Update(0, Frame), Is.EqualTo(0));
        Assert.That(repeat.Update(1, Frame), Is.EqualTo(1));
    }
}

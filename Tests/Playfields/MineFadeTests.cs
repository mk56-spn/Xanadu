using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Systems.Playfields;

namespace Writer.Tests.Playfields;

[TestFixture]
public sealed class MineFadeTests
{
    [Test]
    public void MineFadesInAndOut()
    {
        var mine = new Mine(1_000, Vector2.Zero);

        Assert.That(MineFade.Alpha(mine, 400), Is.Zero);
        Assert.That(MineFade.Alpha(mine, 1_000), Is.EqualTo(1f));
        Assert.That(MineFade.Alpha(mine, 1_300), Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(MineFade.Alpha(mine, 1_601), Is.Zero);
    }
}

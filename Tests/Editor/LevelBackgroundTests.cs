using Godot;
using NUnit.Framework;
using Xanadu.Levels;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class LevelBackgroundTests
{
    [Test]
    public void FormatsOpaqueAndTranslucentColours()
    {
        Assert.That(
            LevelBackground.Format(new Color("#204060")),
            Is.EqualTo("#204060"));
        Assert.That(
            LevelBackground.Format(new Color("#20406080")),
            Is.EqualTo("#20406080"));
    }

    [Test]
    public void ParsesBothStoredForms()
    {
        Assert.That(
            LevelBackground.TryParse("#204060", out Color opaque),
            Is.True);
        Assert.That(opaque, Is.EqualTo(new Color("#204060")));

        Assert.That(
            LevelBackground.TryParse("20406080", out Color translucent),
            Is.True);
        Assert.That(translucent.A, Is.EqualTo(128 / 255f).Within(0.001f));
    }

    [Test]
    public void RejectsValuesThatAreNotHexColours()
    {
        Assert.That(LevelBackground.TryParse("", out _), Is.False);
        Assert.That(LevelBackground.TryParse(null, out _), Is.False);
        Assert.That(LevelBackground.TryParse("#abc", out _), Is.False);
        Assert.That(LevelBackground.TryParse("#zzzzzz", out _), Is.False);
        Assert.That(LevelBackground.TryParse("#1234567", out _), Is.False);
    }

    [Test]
    public void NormalizeKeepsValidColoursAndClearsTheRest()
    {
        Assert.That(LevelBackground.Normalize("204060"), Is.EqualTo("#204060"));
        Assert.That(LevelBackground.Normalize("not a colour"), Is.Empty);
        Assert.That(LevelBackground.Normalize(""), Is.Empty);
    }
}

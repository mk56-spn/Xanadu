using System;
using System.IO;
using NUnit.Framework;
using Xanadu.Settings;

namespace Xanadu.Tests.Settings;

[TestFixture]
public sealed class MapOffsetSettingsTests
{
    private string _path = "";

    [SetUp]
    public void SetUp() =>
        _path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".xmo");

    [TearDown]
    public void TearDown() => File.Delete(_path);

    [Test]
    public void AnUnknownMapHasNoOffset() =>
        Assert.That(new MapOffsetSettings(_path).Get("level"), Is.Zero);

    [Test]
    public void OffsetsSurviveARestartAndStayPerMap()
    {
        var first = new MapOffsetSettings(_path);
        first.Set("a", 12);
        first.Set("b", -7);

        var second = new MapOffsetSettings(_path);

        Assert.That(second.Get("a"), Is.EqualTo(12));
        Assert.That(second.Get("b"), Is.EqualTo(-7));
    }

    [TestCase(1000, MapOffsetSettings.MaxMs)]
    [TestCase(-1000, -MapOffsetSettings.MaxMs)]
    public void OffsetsAreClamped(int asked, int expected) =>
        Assert.That(new MapOffsetSettings(_path).Set("a", asked), Is.EqualTo(expected));

    [Test]
    public void ZeroLeavesNoEntry()
    {
        var settings = new MapOffsetSettings(_path);
        settings.Set("a", 5);
        settings.Set("a", 0);

        Assert.That(new MapOffsetSettings(_path).Get("a"), Is.Zero);
        Assert.That(new FileInfo(_path).Length, Is.EqualTo(8), "just the header");
    }

    [Test]
    public void ADamagedFileIsIgnored()
    {
        File.WriteAllBytes(_path, new byte[] { 1, 2, 3 });
        Assert.That(new MapOffsetSettings(_path).Get("a"), Is.Zero);

        File.WriteAllBytes(_path, new byte[] { (byte)'X', (byte)'M', (byte)'O', 1, 200, 0, 0, 0 });
        Assert.That(new MapOffsetSettings(_path).Get("a"), Is.Zero);
    }

    [Test]
    public void WithoutPersistenceNothingIsWritten()
    {
        var settings = new MapOffsetSettings(persist: false);
        settings.Set("a", 5);

        Assert.That(settings.Get("a"), Is.EqualTo(5));
        Assert.That(File.Exists(_path), Is.False);
    }
}

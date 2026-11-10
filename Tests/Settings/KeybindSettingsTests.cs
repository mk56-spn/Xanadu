using System;
using System.IO;
using NUnit.Framework;
using Xanadu.Settings;

namespace Xanadu.Tests.Settings;

[TestFixture]
public sealed class KeybindSettingsTests
{
    private string _path = null!;

    [SetUp]
    public void SetUp()
    {
        _path = Path.Combine(
            Path.GetTempPath(),
            "writer-keybinds-" + Guid.NewGuid().ToString("N") + ".json");
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(_path))
            File.Delete(_path);
    }

    [Test]
    public void DefaultsMatchTheRequestedLayout()
    {
        var settings = new KeybindSettings(_path);

        Assert.That(settings.Get(KeybindAction.Left), Is.EqualTo(Key.S));
        Assert.That(settings.Get(KeybindAction.Right), Is.EqualTo(Key.D));
        Assert.That(settings.Get(KeybindAction.Up), Is.EqualTo(Key.K));
        Assert.That(settings.Get(KeybindAction.Down), Is.EqualTo(Key.L));
        Assert.That(settings.Get(KeybindAction.Action1), Is.EqualTo(Key.F));
        Assert.That(settings.Get(KeybindAction.Action2), Is.EqualTo(Key.Space));
        Assert.That(settings.Get(KeybindAction.Action3), Is.EqualTo(Key.J));
    }

    [Test]
    public void CapturedKeyIsPersisted()
    {
        var settings = new KeybindSettings(_path);
        settings.BeginCapture(KeybindAction.Left);

        Assert.That(settings.TryCapture(Key.A), Is.True);
        Assert.That(settings.Capturing, Is.Null);
        Assert.That(settings.Get(KeybindAction.Left), Is.EqualTo(Key.A));

        var reloaded = new KeybindSettings(_path);
        Assert.That(reloaded.Get(KeybindAction.Left), Is.EqualTo(Key.A));
        Assert.That(reloaded.Get(KeybindAction.Right), Is.EqualTo(Key.D));
    }

    [Test]
    public void ConflictingKeySwapsTheOtherBinding()
    {
        var settings = new KeybindSettings(_path);
        settings.BeginCapture(KeybindAction.Left);

        Assert.That(settings.TryCapture(Key.D), Is.True);

        Assert.That(settings.Get(KeybindAction.Left), Is.EqualTo(Key.D));
        Assert.That(settings.Get(KeybindAction.Right), Is.EqualTo(Key.S));
    }

    [Test]
    public void EscapeCancelsCapture()
    {
        var settings = new KeybindSettings(_path);
        settings.BeginCapture(KeybindAction.Left);

        Assert.That(settings.TryCapture(Key.Escape), Is.True);
        Assert.That(settings.Capturing, Is.Null);
        Assert.That(settings.Get(KeybindAction.Left), Is.EqualTo(Key.S));
    }

    [Test]
    public void FunctionKeysStayWithTheDebugViews()
    {
        var settings = new KeybindSettings(_path);
        settings.BeginCapture(KeybindAction.Left);

        Assert.That(settings.TryCapture(Key.F6), Is.True, "the press is consumed");
        Assert.That(settings.Capturing, Is.EqualTo(KeybindAction.Left), "but the capture stays open");
        Assert.That(settings.Get(KeybindAction.Left), Is.EqualTo(Key.S));
    }

    [Test]
    public void ModifierKeysDoNotEndCapture()
    {
        var settings = new KeybindSettings(_path);
        settings.BeginCapture(KeybindAction.Left);

        Assert.That(settings.TryCapture(Key.Shift), Is.True);
        Assert.That(settings.Capturing, Is.EqualTo(KeybindAction.Left));
        Assert.That(settings.Get(KeybindAction.Left), Is.EqualTo(Key.S));
    }

    [Test]
    public void KeyLabelsUseReadableNames()
    {
        Assert.That(KeybindSettings.KeyLabel(Key.S), Is.EqualTo("S"));
        Assert.That(KeybindSettings.KeyLabel(Key.Space), Is.EqualTo("SPACE"));
        Assert.That(
            KeybindSettings.LabelFor(KeybindAction.Action3),
            Is.EqualTo("ACTION 3"));
    }
}

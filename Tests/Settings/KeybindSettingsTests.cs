using System;
using System.IO;
using Godot;
using NUnit.Framework;
using Writer.Settings;

namespace Writer.Tests.Settings;

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

        Assert.That(settings.Get(KeybindAction.L1), Is.EqualTo(Key.S));
        Assert.That(settings.Get(KeybindAction.L2), Is.EqualTo(Key.D));
        Assert.That(settings.Get(KeybindAction.L3), Is.EqualTo(Key.F));
        Assert.That(settings.Get(KeybindAction.M), Is.EqualTo(Key.Space));
        Assert.That(settings.Get(KeybindAction.R1), Is.EqualTo(Key.J));
        Assert.That(settings.Get(KeybindAction.R2), Is.EqualTo(Key.K));
        Assert.That(settings.Get(KeybindAction.R3), Is.EqualTo(Key.L));
    }

    [Test]
    public void CapturedKeyIsPersisted()
    {
        var settings = new KeybindSettings(_path);
        settings.BeginCapture(KeybindAction.L1);

        Assert.That(settings.TryCapture(Key.A), Is.True);
        Assert.That(settings.Capturing, Is.Null);
        Assert.That(settings.Get(KeybindAction.L1), Is.EqualTo(Key.A));

        var reloaded = new KeybindSettings(_path);
        Assert.That(reloaded.Get(KeybindAction.L1), Is.EqualTo(Key.A));
        Assert.That(reloaded.Get(KeybindAction.L2), Is.EqualTo(Key.D));
    }

    [Test]
    public void ConflictingKeySwapsTheOtherBinding()
    {
        var settings = new KeybindSettings(_path);
        settings.BeginCapture(KeybindAction.L1);

        Assert.That(settings.TryCapture(Key.D), Is.True);

        Assert.That(settings.Get(KeybindAction.L1), Is.EqualTo(Key.D));
        Assert.That(settings.Get(KeybindAction.L2), Is.EqualTo(Key.S));
    }

    [Test]
    public void EscapeCancelsCapture()
    {
        var settings = new KeybindSettings(_path);
        settings.BeginCapture(KeybindAction.L1);

        Assert.That(settings.TryCapture(Key.Escape), Is.True);
        Assert.That(settings.Capturing, Is.Null);
        Assert.That(settings.Get(KeybindAction.L1), Is.EqualTo(Key.S));
    }

    [Test]
    public void ModifierKeysDoNotEndCapture()
    {
        var settings = new KeybindSettings(_path);
        settings.BeginCapture(KeybindAction.L1);

        Assert.That(settings.TryCapture(Key.Shift), Is.True);
        Assert.That(settings.Capturing, Is.EqualTo(KeybindAction.L1));
        Assert.That(settings.Get(KeybindAction.L1), Is.EqualTo(Key.S));
    }

    [Test]
    public void KeyLabelsUseReadableNames()
    {
        Assert.That(KeybindSettings.KeyLabel(Key.S), Is.EqualTo("S"));
        Assert.That(KeybindSettings.KeyLabel(Key.Space), Is.EqualTo("SPACE"));
        Assert.That(
            KeybindSettings.LabelFor(KeybindAction.R3),
            Is.EqualTo("R3"));
    }
}

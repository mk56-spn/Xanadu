using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Editor;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class EditorPlacementKeybindsTests
{
    [TestCase(Key.Key1, EditorPlacementMode.Notes)]
    [TestCase(Key.Kp1, EditorPlacementMode.Notes)]
    [TestCase(Key.Key2, EditorPlacementMode.Neutral)]
    [TestCase(Key.Kp2, EditorPlacementMode.Neutral)]
    [TestCase(Key.Key3, EditorPlacementMode.Dash)]
    [TestCase(Key.Kp3, EditorPlacementMode.Dash)]
    [TestCase(Key.Key4, EditorPlacementMode.Momentum)]
    [TestCase(Key.Kp4, EditorPlacementMode.Momentum)]
    [TestCase(Key.Key5, EditorPlacementMode.Blocks)]
    [TestCase(Key.Kp5, EditorPlacementMode.Blocks)]
    [TestCase(Key.Key6, EditorPlacementMode.Spikes)]
    [TestCase(Key.Kp6, EditorPlacementMode.Spikes)]
    [TestCase(Key.Key7, EditorPlacementMode.Contact)]
    [TestCase(Key.Kp7, EditorPlacementMode.Contact)]
    [TestCase(Key.Key8, EditorPlacementMode.Mines)]
    [TestCase(Key.Kp8, EditorPlacementMode.Mines)]
    [TestCase(Key.Key9, EditorPlacementMode.FrictionBar)]
    [TestCase(Key.Kp9, EditorPlacementMode.FrictionBar)]
    [TestCase(Key.Key0, EditorPlacementMode.Text)]
    [TestCase(Key.Kp0, EditorPlacementMode.Text)]
    public void MapsDefaultPlacementKeys(
        Key key,
        EditorPlacementMode expected)
    {
        Assert.That(
            EditorPlacementKeybinds.TryGet(key, out EditorPlacementMode mode),
            Is.True);
        Assert.That(mode, Is.EqualTo(expected));
    }

    [Test]
    public void OtherKeysAreNotPlacementShortcuts()
    {
        Assert.That(
            EditorPlacementKeybinds.TryGet(Key.T, out _),
            Is.False);
    }

    [Test]
    public void LabelsMatchTheDefaultKeys()
    {
        Assert.That(
            EditorPlacementKeybinds.LabelFor(EditorPlacementMode.Notes),
            Is.EqualTo("1"));
        Assert.That(
            EditorPlacementKeybinds.LabelFor(EditorPlacementMode.Neutral),
            Is.EqualTo("2"));
        Assert.That(
            EditorPlacementKeybinds.LabelFor(EditorPlacementMode.Dash),
            Is.EqualTo("3"));
        Assert.That(
            EditorPlacementKeybinds.LabelFor(EditorPlacementMode.Momentum),
            Is.EqualTo("4"));
        Assert.That(
            EditorPlacementKeybinds.LabelFor(EditorPlacementMode.Blocks),
            Is.EqualTo("5"));
        Assert.That(
            EditorPlacementKeybinds.LabelFor(EditorPlacementMode.Spikes),
            Is.EqualTo("6"));
        Assert.That(
            EditorPlacementKeybinds.LabelFor(EditorPlacementMode.Contact),
            Is.EqualTo("7"));
        Assert.That(
            EditorPlacementKeybinds.LabelFor(EditorPlacementMode.Mines),
            Is.EqualTo("8"));
        Assert.That(
            EditorPlacementKeybinds.LabelFor(EditorPlacementMode.FrictionBar),
            Is.EqualTo("9"));
    }
}

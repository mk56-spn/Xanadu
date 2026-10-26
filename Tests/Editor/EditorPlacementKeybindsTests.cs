using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Editor;

namespace Writer.Tests.Editor;

[TestFixture]
public sealed class EditorPlacementKeybindsTests
{
    [TestCase(Key.Key1, EditorPlacementMode.Notes)]
    [TestCase(Key.Kp1, EditorPlacementMode.Notes)]
    [TestCase(Key.Key2, EditorPlacementMode.Movement)]
    [TestCase(Key.Kp2, EditorPlacementMode.Movement)]
    [TestCase(Key.Key3, EditorPlacementMode.Blocks)]
    [TestCase(Key.Kp3, EditorPlacementMode.Blocks)]
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
            EditorPlacementKeybinds.LabelFor(EditorPlacementMode.Movement),
            Is.EqualTo("2"));
        Assert.That(
            EditorPlacementKeybinds.LabelFor(EditorPlacementMode.Blocks),
            Is.EqualTo("3"));
    }
}

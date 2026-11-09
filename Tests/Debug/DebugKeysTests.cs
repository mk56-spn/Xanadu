using Godot;
using NUnit.Framework;
using Xanadu.Systems.Input;

namespace Xanadu.Tests.Debug;

[TestFixture]
public sealed class DebugKeysTests
{
    [Test]
    public void TheBandsKeepTheirOrder()
    {
        Assert.That(
            new[] { DebugKeys.FrameRate, DebugKeys.ShaderTuner, DebugKeys.Ecs, DebugKeys.UiTree },
            Is.EqualTo(new[] { Key.F1, Key.F2, Key.F3, Key.F4 }),
            "engine views fill F1-F4");

        Assert.That(DebugKeys.Preview, Is.EqualTo(Key.F5), "preview is its own cross-tool slot");

        Assert.That(
            new[] { DebugKeys.Hitboxes, DebugKeys.HudLayout, DebugKeys.GroundMode },
            Is.EqualTo(new[] { Key.F6, Key.F7, Key.F8 }),
            "gameplay views start after preview and fill F6-F8");

        Assert.That(
            new[] { DebugKeys.EditorHelp, DebugKeys.EditorShaders },
            Is.EqualTo(new[] { Key.F9, Key.F10 }),
            "editor views follow gameplay; F11 is their spare");

        Assert.That(DebugKeys.Screenshot, Is.EqualTo(Key.F12), "the app key stays last");
    }

    [Test]
    public void EveryFunctionKeyIsReservedFromNoteBindings()
    {
        Key[] functionKeys =
        {
            Key.F1, Key.F2, Key.F3, Key.F4, Key.F5, Key.F6,
            Key.F7, Key.F8, Key.F9, Key.F10, Key.F11, Key.F12,
        };
        foreach (Key key in functionKeys)
            Assert.That(DebugKeys.IsReserved(key), Is.True, key.ToString());

        Assert.That(DebugKeys.IsReserved(Key.A), Is.False);
        Assert.That(DebugKeys.IsReserved(Key.F13), Is.False);
    }
}

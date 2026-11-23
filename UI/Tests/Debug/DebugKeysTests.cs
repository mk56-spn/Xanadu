using NUnit.Framework;
using Xanadu.Ui.Systems.Input;

namespace Xanadu.Ui.Tests.Debug;

[TestFixture]
public sealed class DebugKeysTests
{
    [Test]
    public void EveryFunctionKeyIsReservedFromNoteBindings()
    {
        Key[] functionKeys =
        {
            Key.F1, Key.F2, Key.F3, Key.F4, Key.F5, Key.F6,
            Key.F7, Key.F8, Key.F9, Key.F10, Key.F11, Key.F12
        };
        foreach (Key key in functionKeys)
            Assert.That(DebugKeys.IsReserved(key), Is.True, key.ToString());

        Assert.That(DebugKeys.IsReserved(Key.A), Is.False);
        Assert.That(DebugKeys.IsReserved(Key.F13), Is.False);
    }
}

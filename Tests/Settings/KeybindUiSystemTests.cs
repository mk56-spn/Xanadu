using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Settings;
using Xanadu.Systems.Input;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tests.Settings;

[TestFixture]
public sealed class KeybindUiSystemTests
{
    [Test]
    public void ButtonShowsBindingAndCapturePrompt()
    {
        var store = new EntityStore();
        var root = new SystemRoot(store);
        var keybinds = new KeybindSettings(persist: false);
        root.Add(new KeybindUiSystem(keybinds));

        Entity button = store.CreateEntity(
            new Text(""),
            UiCommand.Of(KeybindAction.Action1));

        root.Update(new UpdateTick(1f / 60f, 0));
        Assert.That(button.GetComponent<Text>().Value, Is.EqualTo("S"));

        keybinds.BeginCapture(KeybindAction.Action1);
        root.Update(new UpdateTick(1f / 60f, 0));
        Assert.That(
            button.GetComponent<Text>().Value,
            Is.EqualTo("PRESS A KEY..."));

        keybinds.TryCapture(Key.A);
        root.Update(new UpdateTick(1f / 60f, 0));
        Assert.That(button.GetComponent<Text>().Value, Is.EqualTo("A"));
    }
}

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Settings;
using Writer.Systems.Ui;

namespace Writer.Tests.UI;

[TestFixture]
public sealed class ToggleSystemTests
{
    [Test]
    public void ClickTogglesValueLabelAndSetting()
    {
        var store = new EntityStore();
        var display = new DisplaySettings(apply: false, persist: false);
        Entity toggle = store.CreateEntity(
            new Toggle("V-SYNC: ON", "V-SYNC: OFF", false),
            new Text("V-SYNC: OFF"),
            new VSyncToggle(),
            Tags.Get<Pressed>());

        var root = new SystemRoot(store);
        root.Add(new ToggleSystem());
        root.Add(new VSyncToggleSystem(display));

        root.Update(new UpdateTick(1f / 60f, 0));
        root.Update(new UpdateTick(1f / 60f, 1));

        Assert.That(toggle.GetComponent<Toggle>().Value, Is.True);
        Assert.That(toggle.GetComponent<Text>().Value, Is.EqualTo("V-SYNC: ON"));
        Assert.That(display.VSyncEnabled, Is.True);
    }

    [Test]
    public void HoldingDoesNotFlipRepeatedly()
    {
        var store = new EntityStore();
        Entity toggle = store.CreateEntity(
            new Toggle("ON", "OFF", false),
            new Text("OFF"),
            Tags.Get<Pressed>());

        var root = new SystemRoot(store);
        root.Add(new ToggleSystem());

        root.Update(new UpdateTick(1f / 60f, 0));
        root.Update(new UpdateTick(1f / 60f, 1));
        root.Update(new UpdateTick(1f / 60f, 2));

        Assert.That(toggle.GetComponent<Toggle>().Value, Is.True);
    }
}

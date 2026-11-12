using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Ui;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tests.UI;

[TestFixture]
public sealed class UiToggleTests
{
    [Test]
    public void APressFlipsTheToggleAndItsOwnerHearsTheNativeEvent()
    {
        var store = new EntityStore();
        Entity toggle = store.CreateEntity(new Toggle("ON", "OFF", false), new Text("OFF"));
        var heard = new System.Collections.Generic.List<bool>();
        toggle.OnToggled(heard.Add);

        var root = new SystemRoot(store);
        root.Add(new ToggleSystem());

        toggle.AddTag<Pressed>();
        root.Update(new UpdateTick(1f / 60f, 0));
        root.Update(new UpdateTick(1f / 60f, 1));
        toggle.RemoveTag<Pressed>();
        root.Update(new UpdateTick(1f / 60f, 2));
        toggle.AddTag<Pressed>();
        root.Update(new UpdateTick(1f / 60f, 3));

        Assert.That(heard, Is.EqualTo(new[] { true, false }));
        Assert.That(toggle.GetComponent<Toggle>().Value, Is.False);
    }
}

using Friflo.Engine.ECS;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Systems.Rendering;

namespace Writer.Tests.UI.Rendering;

[TestFixture]
public sealed class UiDrawOrderTests
{
    [Test]
    public void BackgroundDrawsBelowContentAndTextDrawsAbovePanels()
    {
        var store = new EntityStore();
        Entity background = store.CreateEntity(new UiBackground());
        Entity panel = store.CreateEntity(new Rectangle(default));
        Entity text = store.CreateEntity(new Text("Title"));
        Entity button = store.CreateEntity();
        button.AddTag<TakesInput>();

        Assert.That(DrawableInitialiseSystem.LocalZIndex(background),
            Is.EqualTo(-1));
        Assert.That(DrawableInitialiseSystem.LocalZIndex(panel),
            Is.EqualTo(0));
        Assert.That(DrawableInitialiseSystem.LocalZIndex(text),
            Is.EqualTo(1));
        Assert.That(DrawableInitialiseSystem.LocalZIndex(button),
            Is.EqualTo(1));
    }

    [Test]
    public void ADropdownListDrawsAboveContentAndItsOptionsAboveIt()
    {
        var store = new EntityStore();
        Entity owner = store.CreateEntity();
        Entity list = store.CreateEntity();
        list.AddTag<TakesInput>();
        list.AddTag<DropdownListPanel>();
        Entity option = store.CreateEntity(new DropdownOption(owner, 0), new Text("A"));
        Entity ordinary = store.CreateEntity(new Text("Title"));

        int content = DrawableInitialiseSystem.LocalZIndex(ordinary);
        int listZ = DrawableInitialiseSystem.LocalZIndex(list);
        int optionZ = DrawableInitialiseSystem.LocalZIndex(option);

        // Nested text adds its own z to its parent's, so the list must clear two of them.
        Assert.That(listZ, Is.GreaterThan(content * 2));
        Assert.That(optionZ, Is.GreaterThan(listZ));
    }
}

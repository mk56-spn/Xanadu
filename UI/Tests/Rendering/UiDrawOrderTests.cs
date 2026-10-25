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
}

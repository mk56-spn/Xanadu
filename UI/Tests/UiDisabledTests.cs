using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Input;
using Xanadu.Systems.Ui;
using Xanadu.Tests.Support;

namespace Xanadu.Tests.UI;

[TestFixture]
public sealed class UiDisabledTests
{
    [Test]
    public void DisablingClearsInteractionStateAndEnablingRestoresTheControl()
    {
        var store = new EntityStore();
        Entity button = store.CreateEntity(
            new ButtonVisual(Colors.Blue, Colors.White, Colors.White),
            new FillColor(Colors.Blue),
            Tags.Get<Pressed, Hovered, Focused, TextFieldFocused>());

        UiLayout.SetDisabled(button, true);

        Entity disabled = store.GetEntityById(button.Id);
        Assert.That(disabled.Tags.Has<Disabled>(), Is.True);
        Assert.That(disabled.Tags.Has<Pressed>(), Is.False);
        Assert.That(disabled.Tags.Has<Hovered>(), Is.False);
        Assert.That(disabled.Tags.Has<Focused>(), Is.False);
        Assert.That(disabled.Tags.Has<TextFieldFocused>(), Is.False);

        UiLayout.SetDisabled(disabled, false);
        Entity enabled = store.GetEntityById(button.Id);
        Assert.That(enabled.Tags.Has<Disabled>(), Is.False);
    }

    [Test]
    public void DisabledButtonsAreDimmed()
    {
        var store = new EntityStore();
        var visuals = new FakeVisualRidFactory();
        UiBuilder ui = UiBuilder.Begin(store, visuals).Layer(10);
        Entity button = ui.RectEntity(
            Vector2.Zero,
            new Vector2(80, 30),
            Colors.Blue,
            "Press",
            takesInput: true);
        button.AddComponent(ButtonVisual.Create(Colors.Blue));
        UiLayout.SetDisabled(button, true);

        var root = new SystemRoot(store);
        root.Add(new ButtonVisualSystem());
        root.Add(new DisabledVisualSystem(store));
        root.Update(new UpdateTick(1f / 60f, 0));

        Color fill = store.GetEntityById(button.Id).GetComponent<FillColor>().Value;
        Assert.That(fill, Is.EqualTo(Colors.Blue.Darkened(0.55f)));
    }
}

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Ui;
using Position = Xanadu.Ecs.Position;
using Xanadu.Tests.Support;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tests.UI;

[TestFixture]
public sealed class ToastSystemTests
{
    [Test]
    public void ShowsAndHidesATimedMessage()
    {
        var store = new EntityStore();
        var root = new SystemRoot(store);
        var toasts = new ToastSystem(
            store,
            new Vector2(1280, 720),
            new FakeVisualRidFactory());
        root.Add(toasts);

        toasts.Show("Screenshot saved");
        root.Update(new UpdateTick(0.1f, 0));

        Entity toast = toasts.Entity;
        Assert.That(toasts.Active, Is.True);
        Assert.That(toast.GetComponent<Text>().Value, Is.EqualTo("Screenshot saved"));
        Assert.That(toast.GetComponent<Position>().Value.X, Is.EqualTo(640).Within(0.1f));

        for (int i = 0; i < 160; i++)
            root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(toasts.Active, Is.False);
        Assert.That(toast.GetComponent<Text>().Value, Is.Empty);
        Assert.That(UiLayout.IsHidden(toast), Is.True);
    }

    [Test]
    public void AStatusShownFromInsideAQueryLoopAppearsOnTheNextUpdate()
    {
        var store = new EntityStore();
        var root = new SystemRoot(store);
        var toasts = new ToastSystem(
            store,
            new Vector2(1280, 720),
            new FakeVisualRidFactory());
        root.Add(toasts);

        // As the editor's HUD system does: showing must not change the store mid-loop.
        store.CreateEntity(new TextStyle());
        Assert.DoesNotThrow(() =>
            store.Query<TextStyle>().ForEachEntity((ref TextStyle _, Entity _) => toasts.ShowStatus("Saved")));
        root.Update(new UpdateTick(0.05f, 0));

        Entity toast = toasts.Entity;
        Assert.That(toasts.Active, Is.True);
        Assert.That(toast.GetComponent<Text>().Value, Is.EqualTo("Saved"));
    }

    [Test]
    public void QueuesMessagesWhileOneIsVisible()
    {
        var store = new EntityStore();
        var root = new SystemRoot(store);
        var toasts = new ToastSystem(
            store,
            new Vector2(1280, 720),
            new FakeVisualRidFactory());
        root.Add(toasts);

        toasts.Show("First", duration: 0.1f);
        toasts.Show("Second", duration: 0.1f);
        root.Update(new UpdateTick(0.05f, 0));
        Assert.That(
            toasts.Entity.GetComponent<Text>().Value,
            Is.EqualTo("First"));

        // The first is gone after 0.54 s; the second then shows for its own 0.1 s.
        for (int i = 0; i < 40; i++)
            root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(
            toasts.Entity.GetComponent<Text>().Value,
            Is.EqualTo("Second"));
    }


}

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Systems.Ui;
using Position = Writer.Ecs.Position;
using Writer.Tests.Support;

namespace Writer.Tests.Ui;

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

        Entity toast = store.Query<Toast>().ToEntityList()[0];
        Assert.That(toast.GetComponent<Toast>().Active, Is.True);
        Assert.That(toast.GetComponent<Text>().Value, Is.EqualTo("Screenshot saved"));
        Assert.That(toast.GetComponent<Position>().Value.X, Is.EqualTo(640).Within(0.1f));

        for (int i = 0; i < 160; i++)
            root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(toast.GetComponent<Toast>().Active, Is.False);
        Assert.That(toast.GetComponent<Text>().Value, Is.Empty);
        Assert.That(toast.GetComponent<Position>().Value.X, Is.EqualTo(-10_000));
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
            store.Query<Toast>().ToEntityList()[0].GetComponent<Text>().Value,
            Is.EqualTo("First"));

        for (int i = 0; i < 160; i++)
            root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(
            store.Query<Toast>().ToEntityList()[0].GetComponent<Text>().Value,
            Is.EqualTo("Second"));
    }


}

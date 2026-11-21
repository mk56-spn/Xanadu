using Xanadu.Core.Tweens;
using Xanadu.Tests.Support.Fakes;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Ui;
using Position = Xanadu.Core.Position;

namespace Xanadu.Ui.Tests;

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
        Step(root, 0.1f);

        Entity toast = toasts.Entity;
        Assert.That(toasts.Active, Is.True);
        Assert.That(toast.GetComponent<Text>().Value, Is.EqualTo("Screenshot saved"));
        Assert.That(toast.GetComponent<Position>().Value.X, Is.EqualTo(640).Within(0.1f));

        for (int i = 0; i < 160; i++)
            Step(root, 1f / 60f);

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
        Step(root, 0.05f);

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
        Step(root, 0.05f);
        Assert.That(
            toasts.Entity.GetComponent<Text>().Value,
            Is.EqualTo("First"));

        // The first is gone after 0.54 s; the second then shows for its own 0.1 s.
        for (int i = 0; i < 40; i++)
            Step(root, 1f / 60f);

        Assert.That(
            toasts.Entity.GetComponent<Text>().Value,
            Is.EqualTo("Second"));
    }

    /// <summary>One frame: toasts play on the tween clock, so it moves on with the tick.</summary>
    private static void Step(SystemRoot root, float seconds)
    {
        ClipClocks.SetSeconds(ClipClocks.Seconds + seconds);
        root.Update(new UpdateTick(seconds, 0));
    }
}

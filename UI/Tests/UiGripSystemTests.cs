using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Xanadu.Core;
using Xanadu.Tests.Support.Fakes;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Input;
using Xanadu.Ui.Systems.Ui;

namespace Xanadu.Ui.Tests;

/// <summary>Grips: the least reach in range is taken, the held one hears every move wherever the pointer goes, and lets go once.</summary>
[TestFixture]
public sealed class UiGripSystemTests
{
    private EntityStore _store = null!;
    private SystemRoot _root = null!;
    private readonly FakePointerInput _pointer = new();
    private Entity _body, _dot;
    private readonly List<string> _heard = [];

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        UiNode layer = UiNode.Layer(_store, default, 0, new FakeVisualRidFactory());
        _body = layer.Grip(40).At(new Vector2(100, 100)).Entity;
        _dot = layer.Grip(10, Tags.Get<GripDot>()).At(new Vector2(120, 100)).Entity;
        _heard.Clear();
        foreach ((Entity grip, string name) in new[] { (_body, "body"), (_dot, "dot") })
        {
            grip.AddSignalHandler<GripTaken>(signal => _heard.Add($"{name} taken {signal.Event.Pointer.X}"));
            grip.AddSignalHandler<GripPulled>(signal => _heard.Add($"{name} pulled {signal.Event.Pointer.X}"));
            grip.AddSignalHandler<GripLetGo>(_ => _heard.Add($"{name} let go"));
        }

        _root = new SystemRoot(_store);
        _root.Add(new UiGripSystem(_pointer));
    }

    private void Frame(float x, bool pressed)
    {
        _pointer.Current = new PointerInput(new Vector2(x, 100), pressed);
        _root.Update(new UpdateTick(1f / 60f, 0));
    }

    [Test]
    public void TheLeastReachInRangeIsTaken()
    {
        Frame(118, false);
        Assert.That(_dot.Tags.Has<Hovered>(), Is.True);
        Assert.That(_body.Tags.Has<Hovered>(), Is.False);
        Frame(118, true);
        Assert.That(_heard, Is.EqualTo(new[] { "dot taken 118" }));
        Assert.That(_dot.Tags.Has<Pressed>(), Is.True);
    }

    [Test]
    public void AHeldGripFollowsThePointerOutOfReachAndLetsGoOnce()
    {
        Frame(80, true);
        Frame(300, true);
        Frame(300, true);
        Frame(300, false);
        Frame(300, false);
        Assert.That(_heard, Is.EqualTo(new[] { "body taken 80", "body pulled 300", "body let go" }));
        Assert.That(_body.Tags.Has<Pressed>(), Is.False);
    }

    [Test]
    public void APressThatStartedElsewhereTakesNothing()
    {
        Frame(500, true);
        Frame(100, true);
        Frame(100, false);
        Assert.That(_heard, Is.Empty);
    }

    [Test]
    public void AHiddenGripIsNotTaken()
    {
        UiLayout.SetHidden(_dot, true);
        UiLayout.SetHidden(_body, true);
        _store.GetCommandBuffer().Playback();
        Frame(118, true);
        Assert.That(_heard, Is.Empty);
    }
}

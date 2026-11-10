using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Groups;
using Xanadu.Systems.Input;
using Xanadu.Systems.Timeline;
using Xanadu.Tests.Support;
using Xanadu.Timeline;

namespace Xanadu.Tests.UI;

/// <summary>A wheel zoom keeps the time under the pointer still, until the playhead moves out of view.</summary>
[TestFixture]
public sealed class TimelineZoomAboutPointerTests
{
    private sealed class Host : ITimelineHost
    {
        public double NowMs { get; set; } = 5_000;
        public double DurationMs { get; set; } = 10_000;
        public double StepMs => 250;
        public double StepOriginMs => 0;
        public bool Blocked => false;
        public static readonly Rect2 Panel = new(0, 0, 1000, 100);

        public bool TryGetArea(out Rect2 panel, out Rect2 ruler)
        {
            panel = Panel;
            ruler = default;
            return true;
        }

        public IEnumerable<double> MarkerTimesMs() => [];
        public void SeekMs(double ms) => NowMs = ms;
        public void TogglePlay() { }
        public void NoMarker(int direction) { }
    }

    private sealed class Wheel : IWheelSource
    {
        public int Notches { get; set; }
        public bool Control { get; set; } = true;
        public bool Shift => false;

        public int TakeWheel()
        {
            int taken = Notches;
            Notches = 0;
            return taken;
        }
    }

    private EntityStore _store = null!;
    private SystemRoot _root = null!;
    private Host _host = null!;
    private Wheel _wheel = null!;
    private FakePointerInput _pointer = null!;
    private Entity _timeline;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _host = new Host();
        _wheel = new Wheel();
        _pointer = new FakePointerInput();
        _timeline = _store.CreateEntity(new TimelineView(1f), new TimelineHost(_host));
        _root = new SystemRoot(_store);
        _root.Add(new TimelineSystemGroup(_pointer, _wheel));
    }

    private TimeWindow Window =>
        TimeWindow.Of(_timeline.GetComponent<TimelineView>(), (long)_host.NowMs, (long)_host.DurationMs);

    private void ScrollAt(float x, int notches)
    {
        _pointer.Current = new PointerInput(new Vector2(x, 50), false);
        _wheel.Notches = notches;
        _root.Update(new UpdateTick(0.016f, 0.016f));
    }

    private void Frame() => _root.Update(new UpdateTick(0.016f, 0.016f));

    [TestCase(100f)]
    [TestCase(250f)]
    [TestCase(800f)]
    public void TheTimeUnderThePointerStaysUnderItAsItZoomsIn(float x)
    {
        double under = Window.TimeForX(Host.Panel, x);

        ScrollAt(x, 1);
        ScrollAt(x, 1);

        Assert.That(_timeline.GetComponent<TimelineView>().Zoom, Is.GreaterThan(1f));
        Assert.That(Window.TimeForX(Host.Panel, x), Is.EqualTo(under).Within(15));
    }

    [Test]
    public void ZoomingBackOutAtThePointerKeepsItThereToo()
    {
        ScrollAt(200f, 3);
        double under = Window.TimeForX(Host.Panel, 200f);

        ScrollAt(200f, -1);

        Assert.That(Window.TimeForX(Host.Panel, 200f), Is.EqualTo(under).Within(15));
    }

    [Test]
    public void ThePinHoldsWhileThePlayheadStaysPut()
    {
        ScrollAt(100f, 2);
        TimeWindow pinned = Window;

        Frame();
        Frame();

        Assert.That(_timeline.GetComponent<TimelineView>().Pinned, Is.True);
        Assert.That(Window, Is.EqualTo(pinned));
    }

    [Test]
    public void ThePinLetsGoWhenThePlayheadLeavesTheWindow()
    {
        ScrollAt(100f, 1);
        ScrollAt(100f, 1);
        ScrollAt(100f, 1);
        Assert.That(Window.Contains(_host.NowMs), Is.False, "zoomed in away from the playhead");

        _host.NowMs = 9_000;
        Frame();

        Assert.That(_timeline.GetComponent<TimelineView>().Pinned, Is.False);
        Assert.That(Window, Is.EqualTo(TimeWindow.Following(9_000, 10_000, _timeline.GetComponent<TimelineView>().Zoom)));
    }

    [Test]
    public void APlayheadMovingInsideThePinnedWindowKeepsIt()
    {
        ScrollAt(500f, 1);
        TimeWindow pinned = Window;
        _host.NowMs = pinned.Start + pinned.Span / 4;

        Frame();

        Assert.That(_timeline.GetComponent<TimelineView>().Pinned, Is.True);
        Assert.That(Window, Is.EqualTo(pinned));
    }
}

using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Groups;
using Xanadu.Systems.Input;
using Xanadu.Systems.Timeline;
using Xanadu.Tests.Support;

namespace Xanadu.Tests.UI;

[TestFixture]
public sealed class TimelineShortcutsTests
{
    private sealed class Host : ITimelineHost
    {
        public double NowMs { get; set; } = 1_000;
        public double DurationMs { get; set; } = 5_000;
        public double StepMs { get; set; } = 250;
        public double StepOriginMs { get; set; }
        public bool Blocked { get; set; }
        public Rect2 Panel = new(0, 0, 100, 100);
        public Rect2 Ruler = new(0, 0, 100, 10);
        public double[] Markers = { 500, 1_000, 2_000 };
        public int Toggles;
        public int Missed;

        public bool TryGetArea(out Rect2 panel, out Rect2 ruler)
        {
            panel = Panel;
            ruler = Ruler;
            return true;
        }

        public IEnumerable<double> MarkerTimesMs() => Markers;
        public void SeekMs(double ms) => NowMs = ms;
        public void TogglePlay() => Toggles++;
        public void NoMarker(int direction) => Missed = direction;
    }

    private sealed class Wheel : IWheelSource
    {
        public int Notches { get; set; }
        public bool Control { get; set; }
        public bool Shift { get; set; }

        public int TakeWheel()
        {
            int taken = Notches;
            Notches = 0;
            return taken;
        }
    }

    private static double After(TimelineCommand command, Host? given = null)
    {
        Host host = given ?? new Host();
        TimelineShortcuts.Run(host, command);
        return host.NowMs;
    }

    [TestCase(TimelineCommand.SeekBack, 0)]
    [TestCase(TimelineCommand.SeekForward, 2_000)]
    [TestCase(TimelineCommand.StepBack, 750)]
    [TestCase(TimelineCommand.StepForward, 1_250)]
    [TestCase(TimelineCommand.Start, 0)]
    [TestCase(TimelineCommand.End, 5_000)]
    [TestCase(TimelineCommand.PreviousKey, 500)]
    [TestCase(TimelineCommand.NextKey, 2_000)]
    public void CommandsMoveThePlayhead(TimelineCommand command, double expected) =>
        Assert.That(After(command), Is.EqualTo(expected));

    [Test]
    public void SeekingStopsAtTheEnds()
    {
        Assert.That(After(TimelineCommand.SeekBack, new Host { NowMs = 200 }), Is.EqualTo(0));
        Assert.That(After(TimelineCommand.SeekForward, new Host { NowMs = 4_500 }), Is.EqualTo(5_000));
    }

    [Test]
    public void SteppingLandsOnAGridFromItsOrigin() =>
        Assert.That(After(TimelineCommand.StepForward, new Host { NowMs = 1_010, StepOriginMs = 100 }), Is.EqualTo(1_350));

    [Test]
    public void AJumpWithNowhereToGoSaysSo()
    {
        var host = new Host { NowMs = 2_000 };

        TimelineShortcuts.Run(host, TimelineCommand.NextKey);

        Assert.That(host.NowMs, Is.EqualTo(2_000));
        Assert.That(host.Missed, Is.EqualTo(1));
    }

    [Test]
    public void SpaceTogglesPlayback()
    {
        var host = new Host();

        TimelineShortcuts.Run(host, TimelineShortcuts.Find(new KeyChord(Key.Space))!.Value);

        Assert.That(host.Toggles, Is.EqualTo(1));
    }

    [Test]
    public void ModifiersMustMatch() =>
        Assert.That(TimelineShortcuts.Find(new KeyChord(Key.Home, Ctrl: true)), Is.Null);

    private static (float Zoom, int Left) Scroll(Host host, Vector2 pointer, bool control)
    {
        var store = new EntityStore();
        Entity timeline = store.CreateEntity(new TimelineView(2f), new TimelineHost(host));
        var wheel = new Wheel { Notches = 1, Control = control };
        var root = new SystemRoot(store);
        root.Add(new TimelineSystemGroup(new FakePointerInput { Current = new PointerInput(pointer, false, false, false, false) }, wheel));
        root.Update(new UpdateTick(0.016f, 0.016f));
        return (timeline.GetComponent<TimelineView>().Zoom, wheel.Notches);
    }

    [Test]
    public void ControlWheelOverThePanelZooms() =>
        Assert.That(Scroll(new Host(), new Vector2(50, 50), true), Is.EqualTo((4f, 0)));

    [Test]
    public void TheWheelAloneZoomsOnlyOverTheRuler()
    {
        Assert.That(Scroll(new Host(), new Vector2(50, 5), false), Is.EqualTo((4f, 0)));
        Assert.That(Scroll(new Host(), new Vector2(50, 50), false), Is.EqualTo((2f, 1)));
    }

    [Test]
    public void OutsideThePanelOrWhileBlockedTheWheelIsLeftAlone()
    {
        Assert.That(Scroll(new Host(), new Vector2(500, 500), true), Is.EqualTo((2f, 1)));
        Assert.That(Scroll(new Host { Blocked = true }, new Vector2(50, 50), true), Is.EqualTo((2f, 1)));
    }
}

using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Replays;
using Xanadu.Systems.Playfields;

namespace Xanadu.Tests.Playfields;

[TestFixture]
public sealed class PlaySkipSystemTests
{
    private EntityStore _store = null!;
    private SystemRoot _root = null!;
    private Entity _button;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _root = new SystemRoot(_store);
        _root.Add(new PlaySkipSystem());
        _button = _store.CreateEntity();
        _button.AddTag<UiHidden>();
    }

    [Test]
    public void TargetLandsTheLeadBeforeTheFirstObjectAtOrAfterTheStart()
    {
        Assert.That(PlaySkipSystem.SkipTarget([9_000, 12_000], 0), Is.EqualTo(7_000));
        Assert.That(PlaySkipSystem.SkipTarget([1_000, 12_000], 5_000), Is.EqualTo(10_000));
        Assert.That(PlaySkipSystem.SkipTarget([1_000], 5_000), Is.EqualTo(-1));
        Assert.That(PlaySkipSystem.SkipTarget([], 0), Is.EqualTo(-1));
    }

    [TestCase(0, false, Description = "2s entrance: the first note is exactly the lead away")]
    [TestCase(-1, true, Description = "just over 2s")]
    public void ButtonShowsOnlyWhenMoreThanTheLeadRemains(long timeMs, bool shown)
    {
        Entity play = Play(timeMs, skipToMs: 0);

        Frame();

        Assert.That(UiLayout.IsHidden(_button), Is.EqualTo(!shown));
    }

    [Test]
    public void ButtonWaitsForTheCountdown()
    {
        Entity play = Play(0, skipToMs: 5_000);
        play.GetComponent<PlaySession>().Started = false;

        Frame();

        Assert.That(UiLayout.IsHidden(_button), Is.True);
    }

    [Test]
    public void PressSeeksOnceAndHidesTheButton()
    {
        Entity play = Play(1_000, skipToMs: 5_000);
        play.AddComponent(ReplayRecorder.Create());
        Frame();
        _button.AddTag<Pressed>();

        Frame();

        Assert.That(play.GetComponent<Playhead>().SeekRequestMs, Is.EqualTo(5_000));
        Assert.That(play.GetComponent<ReplayRecorder>().SkipFromMs, Is.EqualTo(1_000));
        Assert.That(UiLayout.IsHidden(_button), Is.False);

        play.GetComponent<Playhead>().SeekRequestMs = -1;
        play.GetComponent<Playhead>().TimeMs = 2_000;
        Frame();

        Assert.That(play.GetComponent<Playhead>().SeekRequestMs, Is.EqualTo(-1));
        Assert.That(UiLayout.IsHidden(_button), Is.True);
    }

    [Test]
    public void EnterSkips()
    {
        Entity play = Play(1_000, skipToMs: 5_000);
        play.GetComponent<PlaySession>().SkipRequested = true;

        Frame();

        Assert.That(play.GetComponent<Playhead>().SeekRequestMs, Is.EqualTo(5_000));
    }

    [Test]
    public void EnterDuringTheCountdownIsNotHeldForLater()
    {
        Entity play = Play(1_000, skipToMs: 5_000);
        play.GetComponent<PlaySession>().Started = false;
        play.GetComponent<PlaySession>().SkipRequested = true;
        Frame();

        play.GetComponent<PlaySession>().Started = true;
        Frame();

        Assert.That(play.GetComponent<Playhead>().SeekRequestMs, Is.EqualTo(-1));
    }

    [Test]
    public void EnterCannotSkipDuringAReplay()
    {
        Entity play = Play(1_000, skipToMs: 5_000);
        play.AddComponent(new ReplayPlayer(Recorded(skipFromMs: -1)));
        play.GetComponent<PlaySession>().SkipRequested = true;

        Frame();

        Assert.That(play.GetComponent<Playhead>().SeekRequestMs, Is.EqualTo(-1));
    }

    [Test]
    public void PausedRunCannotSkip()
    {
        Entity play = Play(1_000, skipToMs: 5_000);
        play.AddTag<PlayPaused>();
        _button.AddTag<Pressed>();

        Frame();

        Assert.That(play.GetComponent<Playhead>().SeekRequestMs, Is.EqualTo(-1));
    }

    [Test]
    public void ReplayRepeatsTheRecordedSkipAndNeverShowsTheButton()
    {
        Entity play = Play(900, skipToMs: 5_000);
        play.AddComponent(new ReplayPlayer(Recorded(skipFromMs: 1_000)));

        Frame();
        Assert.That(play.GetComponent<Playhead>().SeekRequestMs, Is.EqualTo(-1));
        Assert.That(UiLayout.IsHidden(_button), Is.True);

        play.GetComponent<Playhead>().TimeMs = 1_016;
        Frame();
        Assert.That(play.GetComponent<Playhead>().SeekRequestMs, Is.EqualTo(5_000));
    }

    [Test]
    public void ReplayOfAnUnskippedRunPlaysThrough()
    {
        Entity play = Play(1_000, skipToMs: 5_000);
        play.AddComponent(new ReplayPlayer(Recorded(skipFromMs: -1)));

        Frame();

        Assert.That(play.GetComponent<Playhead>().SeekRequestMs, Is.EqualTo(-1));
    }

    private Entity Play(long timeMs, long skipToMs)
    {
        Entity play = _store.CreateEntity(
            new Playhead(timeMs, 120, 0, true),
            new PlaySession(default, skipToMs: skipToMs, skipButton: _button));
        play.GetComponent<Playhead>().DurationMs = 60_000;
        play.GetComponent<PlaySession>().Started = true;
        return play;
    }

    private static Replay Recorded(long skipFromMs) =>
        new("id", "name", "", "", DateTimeOffset.UtcNow, 0, default, [], skipFromMs);

    private void Frame() => _root.Update(new UpdateTick(1f / 60f, 0));
}

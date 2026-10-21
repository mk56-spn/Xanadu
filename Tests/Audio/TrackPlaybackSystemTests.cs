using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Systems.Audio;

namespace Writer.Tests.Audio;

[TestFixture]
public sealed class TrackPlaybackSystemTests
{
    private EntityStore _store = null!;
    private FakeClock _clock = null!;
    private FakeAudio _audio = null!;
    private SystemRoot _root = null!;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _clock = new FakeClock();
        _audio = new FakeAudio();
        _root = new SystemRoot(_store);
        _root.Add(new TrackPlaybackSystem(_audio, _clock));
    }

    [Test]
    public void StartsAudioOnceAndAdvancesTimer()
    {
        Entity track = AddTrack(durationMs: 1000);

        Frame();
        Assert.That(_audio.PlayCount, Is.EqualTo(1));
        Assert.That(track.GetComponent<TrackProgress>().Started, Is.True);

        _clock.NowMicros = 500_000;
        Frame();

        Assert.That(_audio.PlayCount, Is.EqualTo(1));
        Assert.That(track.GetComponent<TrackProgress>().NowMs, Is.EqualTo(500));
    }

    [Test]
    public void SpeedPercentScalesChartTime()
    {
        Entity track = AddTrack(durationMs: 1000, speedPercent: 50);

        Frame();
        _clock.NowMicros = 1_000_000;
        Frame();

        Assert.That(track.GetComponent<TrackProgress>().NowMs, Is.EqualTo(500));
    }

    [Test]
    public void AudioLengthReplacesPlaceholderDuration()
    {
        _audio.LengthSeconds = 12.5f;
        Entity track = AddTrack(durationMs: 0);

        Frame();

        Assert.That(track.GetComponent<TrackProgress>().DurationMs, Is.EqualTo(12_500));
    }

    [Test]
    public void FinishingStopsAudioAndClampsProgress()
    {
        Entity track = AddTrack(durationMs: 1000);

        Frame();
        _clock.NowMicros = 1_500_000;
        Frame();

        TrackProgress progress = track.GetComponent<TrackProgress>();
        Assert.That(progress.NowMs, Is.EqualTo(1000));
        Assert.That(progress.Finished, Is.True);
        Assert.That(_audio.Stopped, Is.True);
    }

    private Entity AddTrack(long durationMs, int speedPercent = 100) =>
        _store.CreateEntity(
            new TrackProgress(
                "res://test-track.wav",
                durationMs,
                bpm: 128,
                speedPercent: speedPercent));

    private void Frame() => _root.Update(new UpdateTick(1f / 60f, 0));

    private sealed class FakeClock : ITrackClock
    {
        public long NowMicros { get; set; }
    }

    private sealed class FakeAudio : ITrackAudio
    {
        public int PlayCount { get; private set; }

        public bool Stopped { get; private set; }

        public bool IsPlaying { get; private set; }

        public float PositionSeconds => 0;

        public float LengthSeconds { get; set; }

        public bool Play(string audioPath, float volume)
        {
            PlayCount++;
            Stopped = false;
            IsPlaying = true;
            return true;
        }

        public void Stop()
        {
            Stopped = true;
            IsPlaying = false;
        }

        public void Dispose() { }
    }
}

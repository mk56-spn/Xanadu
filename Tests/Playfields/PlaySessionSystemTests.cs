using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Systems.Audio;
using Writer.Systems.Playfields;

namespace Writer.Tests.Playfields;

[TestFixture]
public sealed class PlaySessionSystemTests
{
    [Test]
    public void CountdownShowsThreeTwoOneGoBeforePlayback()
    {
        var store = new EntityStore();
        var root = new SystemRoot(store);
        var audio = new FakeAudio();
        root.Add(new PlaySessionSystem(audio));

        Entity status = store.CreateEntity(new Text("LOADING..."));
        Entity countdown = store.CreateEntity(new Text(""));
        Entity play = store.CreateEntity(
            new Playhead(0, 120, 0, false),
            new PlaySession(status, countdown, lastNoteMs: 4_000));

        ref Playhead playhead = ref play.GetComponent<Playhead>();
        playhead.DurationMs = 5_000;
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(countdown.GetComponent<Text>().Value, Is.EqualTo("3"));
        Assert.That(status.GetComponent<Text>().Value, Is.EqualTo("GET READY"));
        Assert.That(playhead.Playing, Is.False);

        Advance(root, 1.05f);
        Assert.That(countdown.GetComponent<Text>().Value, Is.EqualTo("2"));

        Advance(root, 1.05f);
        Assert.That(countdown.GetComponent<Text>().Value, Is.EqualTo("1"));

        Advance(root, 1.0f);
        Assert.That(countdown.GetComponent<Text>().Value, Is.EqualTo("GO!"));
        Assert.That(playhead.Playing, Is.True);
        Assert.That(play.GetComponent<PlaySession>().Started, Is.True);
        Assert.That(status.GetComponent<Text>().Value, Is.EqualTo("PLAYING"));

        Advance(root, 0.7f);
        Assert.That(countdown.GetComponent<Text>().Value, Is.Empty);
    }

    [Test]
    public void FadesOutAndFinishesAtTheLastNote()
    {
        var store = new EntityStore();
        var root = new SystemRoot(store);
        var audio = new FakeAudio();
        root.Add(new PlaySessionSystem(audio));

        Entity status = store.CreateEntity(new Text("LOADING..."));
        Entity countdown = store.CreateEntity(new Text(""));
        Entity play = store.CreateEntity(
            new Playhead(0, 120, 0, false),
            new PlaySession(status, countdown, lastNoteMs: 1_000));

        ref Playhead playhead = ref play.GetComponent<Playhead>();
        playhead.DurationMs = 5_000;
        root.Update(new UpdateTick(1f / 60f, 0));
        Advance(root, 3.1f);

        Assert.That(playhead.Playing, Is.True);
        Assert.That(playhead.SeekRequestMs, Is.EqualTo(0));
        Assert.That(play.GetComponent<PlaySession>().Started, Is.True);

        playhead.TimeMs = 1_000;
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(play.GetComponent<PlaySession>().Ending, Is.True);
        Assert.That(play.GetComponent<PlaySession>().Finished, Is.False);
        Assert.That(status.GetComponent<Text>().Value, Is.EqualTo("FINISHING"));
        Assert.That(audio.Volume, Is.LessThan(0.8f));

        Advance(root, 2.0f);

        Assert.That(play.GetComponent<PlaySession>().Finished, Is.True);
        Assert.That(playhead.Playing, Is.False);
        Assert.That(status.GetComponent<Text>().Value, Is.EqualTo("FINISHED"));
        Assert.That(audio.Volume, Is.Zero);
        Assert.That(audio.PauseCount, Is.GreaterThan(0));
    }

    [Test]
    public void FadeDoesNotStartBeforeTheLastNote()
    {
        var store = new EntityStore();
        var root = new SystemRoot(store);
        var audio = new FakeAudio();
        root.Add(new PlaySessionSystem(audio));

        Entity status = store.CreateEntity(new Text("LOADING..."));
        Entity countdown = store.CreateEntity(new Text(""));
        Entity play = store.CreateEntity(
            new Playhead(0, 120, 0, false),
            new PlaySession(status, countdown, lastNoteMs: 4_000));

        ref Playhead playhead = ref play.GetComponent<Playhead>();
        playhead.DurationMs = 5_000;
        root.Update(new UpdateTick(1f / 60f, 0));
        Advance(root, 3.1f);

        playhead.TimeMs = 3_999;
        root.Update(new UpdateTick(1f / 60f, 0));
        Assert.That(play.GetComponent<PlaySession>().Ending, Is.False);

        playhead.TimeMs = 4_000;
        root.Update(new UpdateTick(1f / 60f, 0));
        Assert.That(play.GetComponent<PlaySession>().Ending, Is.True);
    }

    [Test]
    public void PausedPlayfieldDoesNotStartTheCountdown()
    {
        var store = new EntityStore();
        var root = new SystemRoot(store);
        var audio = new FakeAudio();
        root.Add(new PlaySessionSystem(audio));

        Entity status = store.CreateEntity(new Text("LOADING..."));
        Entity countdown = store.CreateEntity(new Text(""));
        Entity play = store.CreateEntity(
            new Playhead(0, 120, 0, false),
            new PlaySession(status, countdown),
            Tags.Get<PlayPaused>());

        ref Playhead playhead = ref play.GetComponent<Playhead>();
        playhead.DurationMs = 5_000;
        Advance(root, 4f);

        Assert.That(playhead.Playing, Is.False);
        Assert.That(play.GetComponent<PlaySession>().Started, Is.False);
        Assert.That(countdown.GetComponent<Text>().Value, Is.Empty);
    }

    private static void Advance(SystemRoot root, float seconds)
    {
        int frames = (int)MathF.Ceiling(seconds * 60f);
        for (int i = 0; i < frames; i++)
            root.Update(new UpdateTick(1f / 60f, 0));
    }

    private sealed class FakeAudio : ITrackAudio
    {
        public float Volume { get; private set; } = 0.8f;

        public int PauseCount { get; private set; }

        public bool IsPlaying { get; private set; }

        public float PositionSeconds => 0;

        public float LengthSeconds => 0;

        public bool Play(string audioPath, float volume) => Load(audioPath, volume);

        public bool Load(string audioPath, float volume)
        {
            Volume = volume;
            return true;
        }

        public bool Resume()
        {
            IsPlaying = true;
            return true;
        }

        public void Pause()
        {
            PauseCount++;
            IsPlaying = false;
        }

        public void Seek(float seconds) { }

        public void SetVolume(float volume) => Volume = volume;

        public void Stop() => IsPlaying = false;

        public void Dispose() { }
    }
}

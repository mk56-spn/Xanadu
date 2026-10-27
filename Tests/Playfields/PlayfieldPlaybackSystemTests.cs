using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Audio;
using Xanadu.Systems.Playfields;
using Xanadu.Tests.Support;

namespace Xanadu.Tests.Playfields;

[TestFixture]
public sealed class PlayfieldPlaybackSystemTests
{
    [Test]
    public void LoadsSongDurationAndTracksPlayhead()
    {
        var store = new EntityStore();
        var audio = new RecordingTrackAudio { LengthSeconds = 12f };
        var root = new SystemRoot(store);
        root.Add(new PlayfieldPlaybackSystem(audio));

        Entity editor = store.CreateEntity(
            new LevelInfo("Test", "res://song.mp3"),
            new Playhead(0, 120, 0, false));

        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(audio.LoadedPath, Is.EqualTo("res://song.mp3"));
        Assert.That(editor.GetComponent<Playhead>().DurationMs, Is.EqualTo(12_000));

        ref Playhead playhead = ref editor.GetComponent<Playhead>();
        playhead.Playing = true;
        audio.PositionSeconds = 2.5f;
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(editor.GetComponent<Playhead>().TimeMs, Is.EqualTo(2_500));
        Assert.That(audio.ResumeCount, Is.EqualTo(1));
    }

    [Test]
    public void SeekIsClampedToDuration()
    {
        var store = new EntityStore();
        var audio = new RecordingTrackAudio { LengthSeconds = 5f };
        var root = new SystemRoot(store);
        root.Add(new PlayfieldPlaybackSystem(audio));

        Entity editor = store.CreateEntity(
            new LevelInfo("Test", "res://song.mp3"),
            new Playhead(0, 120, 0, false));

        root.Update(new UpdateTick(1f / 60f, 0));

        ref Playhead playhead = ref editor.GetComponent<Playhead>();
        playhead.SeekRequestMs = 9_000;
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(audio.SeekSeconds, Is.EqualTo(5f));
        Assert.That(editor.GetComponent<Playhead>().TimeMs, Is.EqualTo(5_000));
    }

    [Test]
    public void AudioRevisionReloadsSamePath()
    {
        var store = new EntityStore();
        var audio = new RecordingTrackAudio { LengthSeconds = 5f };
        var root = new SystemRoot(store);
        root.Add(new PlayfieldPlaybackSystem(audio));

        Entity editor = store.CreateEntity(
            new LevelInfo("Test", "res://song.mp3"),
            new Playhead(0, 120, 0, false));

        root.Update(new UpdateTick(1f / 60f, 0));
        ref Playhead playhead = ref editor.GetComponent<Playhead>();
        playhead.AudioRevision++;
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(audio.LoadCount, Is.EqualTo(2));
        Assert.That(playhead.DurationMs, Is.EqualTo(5_000));
    }

    [Test]
    public void PlaybackStopsAtEndWithoutRestarting()
    {
        var store = new EntityStore();
        var audio = new RecordingTrackAudio { LengthSeconds = 5f };
        var root = new SystemRoot(store);
        root.Add(new PlayfieldPlaybackSystem(audio));

        Entity editor = store.CreateEntity(
            new LevelInfo("Test", "res://song.mp3"),
            new Playhead(0, 120, 0, false));

        root.Update(new UpdateTick(1f / 60f, 0));
        ref Playhead playhead = ref editor.GetComponent<Playhead>();
        playhead.Playing = true;
        playhead.TimeMs = playhead.DurationMs;

        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(playhead.Playing, Is.False);
        Assert.That(playhead.TimeMs, Is.EqualTo(5_000));
        Assert.That(audio.ResumeCount, Is.EqualTo(0));
        Assert.That(audio.PauseCount, Is.EqualTo(1));
    }

    [Test]
    public void LoadFailureLeavesDurationZeroAndStopsPlayback()
    {
        var store = new EntityStore();
        var audio = new RecordingTrackAudio { LengthSeconds = 5f, LoadSucceeds = false };
        var root = new SystemRoot(store);
        root.Add(new PlayfieldPlaybackSystem(audio));

        Entity editor = store.CreateEntity(
            new LevelInfo("Test", "res://missing.mp3"),
            new Playhead(0, 120, 0, true));

        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(editor.GetComponent<Playhead>().DurationMs, Is.EqualTo(0));
        Assert.That(editor.GetComponent<Playhead>().Playing, Is.False);
    }

    [Test]
    public void RemovingPlayheadStopsAudio()
    {
        var store = new EntityStore();
        var audio = new RecordingTrackAudio { LengthSeconds = 5f };
        var root = new SystemRoot(store);
        root.Add(new PlayfieldPlaybackSystem(audio));

        Entity editor = store.CreateEntity(
            new LevelInfo("Test", "res://song.mp3"),
            new Playhead(0, 120, 0, false));

        root.Update(new UpdateTick(1f / 60f, 0));

        ref Playhead playhead = ref editor.GetComponent<Playhead>();
        playhead.Playing = true;
        root.Update(new UpdateTick(1f / 60f, 0));
        Assert.That(audio.IsPlaying, Is.True);

        editor.DeleteEntity();
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(audio.StopCount, Is.EqualTo(1));
        Assert.That(audio.IsPlaying, Is.False);
    }

    [Test]
    public void PausingStopsAudio()
    {
        var store = new EntityStore();
        var audio = new RecordingTrackAudio { LengthSeconds = 5f };
        var root = new SystemRoot(store);
        root.Add(new PlayfieldPlaybackSystem(audio));

        Entity editor = store.CreateEntity(
            new LevelInfo("Test", "res://song.mp3"),
            new Playhead(0, 120, 0, false));

        root.Update(new UpdateTick(1f / 60f, 0));

        ref Playhead playhead = ref editor.GetComponent<Playhead>();
        playhead.Playing = true;
        root.Update(new UpdateTick(1f / 60f, 0));
        playhead.Playing = false;
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(audio.PauseCount, Is.GreaterThan(0));
        Assert.That(audio.IsPlaying, Is.False);
    }


}

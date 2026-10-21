using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Systems.Audio;
using Writer.Systems.Editor;

namespace Writer.Tests.Editor;

[TestFixture]
public sealed class EditorPlaybackSystemTests
{
    [Test]
    public void LoadsSongDurationAndTracksPlayhead()
    {
        var store = new EntityStore();
        var audio = new FakeAudio { LengthSeconds = 12f };
        var root = new SystemRoot(store);
        root.Add(new EditorPlaybackSystem(audio));

        Entity editor = store.CreateEntity(
            new LevelInfo("Test", "res://song.mp3"),
            new EditorPlayhead(0, 120, 0, false));

        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(audio.LoadedPath, Is.EqualTo("res://song.mp3"));
        Assert.That(editor.GetComponent<EditorPlayhead>().DurationMs, Is.EqualTo(12_000));

        ref EditorPlayhead playhead = ref editor.GetComponent<EditorPlayhead>();
        playhead.Playing = true;
        audio.PositionSeconds = 2.5f;
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(editor.GetComponent<EditorPlayhead>().TimeMs, Is.EqualTo(2_500));
        Assert.That(audio.ResumeCount, Is.EqualTo(1));
    }

    [Test]
    public void SeekIsClampedToDuration()
    {
        var store = new EntityStore();
        var audio = new FakeAudio { LengthSeconds = 5f };
        var root = new SystemRoot(store);
        root.Add(new EditorPlaybackSystem(audio));

        Entity editor = store.CreateEntity(
            new LevelInfo("Test", "res://song.mp3"),
            new EditorPlayhead(0, 120, 0, false));

        root.Update(new UpdateTick(1f / 60f, 0));

        ref EditorPlayhead playhead = ref editor.GetComponent<EditorPlayhead>();
        playhead.SeekRequestMs = 9_000;
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(audio.SeekSeconds, Is.EqualTo(5f));
        Assert.That(editor.GetComponent<EditorPlayhead>().TimeMs, Is.EqualTo(5_000));
    }

    [Test]
    public void PausingStopsAudio()
    {
        var store = new EntityStore();
        var audio = new FakeAudio { LengthSeconds = 5f };
        var root = new SystemRoot(store);
        root.Add(new EditorPlaybackSystem(audio));

        Entity editor = store.CreateEntity(
            new LevelInfo("Test", "res://song.mp3"),
            new EditorPlayhead(0, 120, 0, false));

        root.Update(new UpdateTick(1f / 60f, 0));

        ref EditorPlayhead playhead = ref editor.GetComponent<EditorPlayhead>();
        playhead.Playing = true;
        root.Update(new UpdateTick(1f / 60f, 0));
        playhead.Playing = false;
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(audio.PauseCount, Is.GreaterThan(0));
        Assert.That(audio.IsPlaying, Is.False);
    }

    private sealed class FakeAudio : ITrackAudio
    {
        public string? LoadedPath { get; private set; }

        public int ResumeCount { get; private set; }

        public int PauseCount { get; private set; }

        public float SeekSeconds { get; private set; }

        public float LengthSeconds { get; set; }

        public float PositionSeconds { get; set; }

        public bool IsPlaying { get; private set; }

        public bool Play(string audioPath, float volume) => Load(audioPath, volume);

        public bool Load(string audioPath, float volume)
        {
            LoadedPath = audioPath;
            return true;
        }

        public bool Resume()
        {
            ResumeCount++;
            IsPlaying = true;
            return true;
        }

        public void Pause()
        {
            PauseCount++;
            IsPlaying = false;
        }

        public void Seek(float seconds) => SeekSeconds = seconds;

        public void Stop() => IsPlaying = false;

        public void Dispose() { }
    }
}

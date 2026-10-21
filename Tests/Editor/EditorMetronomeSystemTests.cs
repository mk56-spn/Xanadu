using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Systems.Audio;
using Writer.Systems.Editor;

namespace Writer.Tests.Editor;

[TestFixture]
public sealed class EditorMetronomeSystemTests
{
    [Test]
    public void ClicksOncePerBeat()
    {
        var store = new EntityStore();
        var audio = new FakeAudio();
        var root = new SystemRoot(store);
        root.Add(new EditorMetronomeSystem(audio));

        Entity editor = store.CreateEntity(
            new Playhead(0, 120, 0, true, durationMs: 5_000),
            new EditorMetronome(true));

        root.Update(new UpdateTick(1f / 60f, 0));
        Assert.That(audio.ResumeCount, Is.EqualTo(1));

        ref Playhead playhead = ref editor.GetComponent<Playhead>();
        playhead.TimeMs = 100;
        root.Update(new UpdateTick(1f / 60f, 0));
        Assert.That(audio.ResumeCount, Is.EqualTo(1));

        playhead.TimeMs = 500;
        root.Update(new UpdateTick(1f / 60f, 0));
        Assert.That(audio.ResumeCount, Is.EqualTo(2));
        Assert.That(audio.LoadCount, Is.EqualTo(1));
    }

    [Test]
    public void DisabledMetronomeDoesNotClick()
    {
        var store = new EntityStore();
        var audio = new FakeAudio();
        var root = new SystemRoot(store);
        root.Add(new EditorMetronomeSystem(audio));

        store.CreateEntity(
            new Playhead(0, 120, 0, true, durationMs: 5_000),
            new EditorMetronome(false));

        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(audio.ResumeCount, Is.EqualTo(0));
    }

    private sealed class FakeAudio : ITrackAudio
    {
        public int ResumeCount { get; private set; }

        public int LoadCount { get; private set; }

        public bool IsPlaying { get; private set; }

        public float PositionSeconds => 0;

        public float LengthSeconds => 0.04f;

        public bool Play(string audioPath, float volume) => Load(audioPath, volume);

        public bool Load(string audioPath, float volume)
        {
            LoadCount++;
            return true;
        }

        public bool Resume()
        {
            ResumeCount++;
            IsPlaying = true;
            return true;
        }

        public void Pause() => IsPlaying = false;

        public void Seek(float seconds) { }

        public void Stop() => IsPlaying = false;

        public void Dispose() { }
    }
}

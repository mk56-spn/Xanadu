using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Audio;
using Xanadu.Systems.Editor;
using Xanadu.Tests.Support;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class EditorMetronomeSystemTests
{
    [Test]
    public void ClicksOncePerBeat()
    {
        var store = new EntityStore();
        var audio = new RecordingTrackAudio();
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
        var audio = new RecordingTrackAudio();
        var root = new SystemRoot(store);
        root.Add(new EditorMetronomeSystem(audio));

        store.CreateEntity(
            new Playhead(0, 120, 0, true, durationMs: 5_000),
            new EditorMetronome(false));

        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(audio.ResumeCount, Is.EqualTo(0));
    }


}

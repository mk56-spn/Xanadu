using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Systems.Editor;

namespace Writer.Tests.Editor;

[TestFixture]
public sealed class PlaySessionSystemTests
{
    [Test]
    public void StartsPlaybackOnceDurationIsKnownAndReportsFinish()
    {
        var store = new EntityStore();
        var root = new SystemRoot(store);
        root.Add(new PlaySessionSystem());

        Entity status = store.CreateEntity(new Text("LOADING..."));
        Entity play = store.CreateEntity(
            new EditorPlayhead(0, 120, 0, false),
            new PlaySession(status));

        root.Update(new UpdateTick(1f / 60f, 0));

        ref EditorPlayhead playhead = ref play.GetComponent<EditorPlayhead>();
        playhead.DurationMs = 5_000;
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(playhead.Playing, Is.True);
        Assert.That(playhead.SeekRequestMs, Is.EqualTo(0));
        Assert.That(play.GetComponent<PlaySession>().Started, Is.True);
        Assert.That(status.GetComponent<Text>().Value, Is.EqualTo("PLAYING"));

        playhead.TimeMs = 5_000;
        playhead.Playing = false;
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(play.GetComponent<PlaySession>().Finished, Is.True);
        Assert.That(
            status.GetComponent<Text>().Value,
            Is.EqualTo("FINISHED - BACK TO SONG SELECT"));
    }
}

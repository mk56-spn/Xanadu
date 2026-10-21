using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Systems.Playfields;

namespace Writer.Tests.Playfields;

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
            new Playhead(0, 120, 0, false),
            new PlaySession(status));

        root.Update(new UpdateTick(1f / 60f, 0));

        ref Playhead playhead = ref play.GetComponent<Playhead>();
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

    [Test]
    public void PausedPlayfieldDoesNotAutoStart()
    {
        var store = new EntityStore();
        var root = new SystemRoot(store);
        root.Add(new PlaySessionSystem());

        Entity status = store.CreateEntity(new Text("LOADING..."));
        Entity play = store.CreateEntity(
            new Playhead(0, 120, 0, false),
            new PlaySession(status),
            Tags.Get<PlayPaused>());

        ref Playhead playhead = ref play.GetComponent<Playhead>();
        playhead.DurationMs = 5_000;
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(playhead.Playing, Is.False);
        Assert.That(play.GetComponent<PlaySession>().Started, Is.False);
    }
}

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Timeline;
using Xanadu.Systems.Editor;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class EditorTimelineSystemTests
{
    [Test]
    public void DefaultsToTheZoomClosestToSixSeconds()
    {
        var store = new EntityStore();
        Entity editor = store.CreateEntity(
            new Playhead(0, 120, 0, false, durationMs: 180_000),
            new TimelineView(1f));

        var root = new SystemRoot(store);
        root.Add(new EditorTimelineSystem());
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(editor.GetComponent<TimelineView>().Zoom, Is.EqualTo(32f));
        Assert.That(editor.GetComponent<TimelineView>().AutoZoomed, Is.True);
    }

    [Test]
    public void DoesNotOverrideAUserChosenZoom()
    {
        var store = new EntityStore();
        Entity editor = store.CreateEntity(
            new Playhead(0, 120, 0, false, durationMs: 180_000),
            new TimelineView(8f));

        ref TimelineView timeline = ref editor.GetComponent<TimelineView>();
        timeline.AutoZoomed = true;

        var root = new SystemRoot(store);
        root.Add(new EditorTimelineSystem());
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(timeline.Zoom, Is.EqualTo(8f));
    }

    [Test]
    public void ClosestZoomUsesClosestVisibleWindow()
    {
        Assert.That(TimelineZoom.ClosestForDuration(24_000), Is.EqualTo(4f));
        Assert.That(TimelineZoom.ClosestForDuration(60_000), Is.EqualTo(8f));
        Assert.That(TimelineZoom.ClosestForDuration(180_000), Is.EqualTo(32f));
    }
}

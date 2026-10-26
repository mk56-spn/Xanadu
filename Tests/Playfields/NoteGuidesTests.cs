using Godot;
using NUnit.Framework;
using Writer.Systems.Playfields;

namespace Writer.Tests.Playfields;

[TestFixture]
public sealed class NoteGuidesTests
{
    private const float Radius = 10f;
    private const float Hub = Radius * NoteGuides.HubRadiusScale;

    private static NoteGuides.Point Note(long time, float x, float y, float alpha = 1f) =>
        new(time, new Vector2(x, y), alpha);

    private static bool Close(Vector2 actual, Vector2 expected) => actual.DistanceTo(expected) < 0.001f;

    [Test]
    public void ConsecutiveNotesAreJoinedRimToRimInTimeOrder()
    {
        // Given out of order: the guide still runs from the earlier note to the later one.
        NoteGuides.Layout layout = NoteGuides.Build(new[] { Note(200, 100, 0), Note(100, 0, 0) }, Radius);

        Assert.That(layout.Guides, Has.Count.EqualTo(1));
        Assert.That(layout.Guides[0].From, Is.EqualTo(new Vector2(10, 0)).Using<Vector2>(Close));
        Assert.That(layout.Guides[0].To, Is.EqualTo(new Vector2(90, 0)).Using<Vector2>(Close));
        Assert.That(layout.Hubs, Is.Empty);
    }

    [Test]
    public void SimultaneousNotesMeetAtAHubWithASpokeToEach()
    {
        NoteGuides.Layout layout = NoteGuides.Build(new[] { Note(100, -50, 0), Note(100, 50, 0) }, Radius);

        Assert.That(layout.Hubs, Has.Count.EqualTo(1));
        Assert.That(layout.Hubs[0].Center, Is.EqualTo(Vector2.Zero).Using<Vector2>(Close));
        Assert.That(layout.Spokes, Has.Count.EqualTo(2));
        Assert.That(layout.Spokes[0].From, Is.EqualTo(new Vector2(-Hub, 0)).Using<Vector2>(Close));
        Assert.That(layout.Spokes[0].To, Is.EqualTo(new Vector2(-40, 0)).Using<Vector2>(Close));
        Assert.That(layout.Guides, Is.Empty);
    }

    [Test]
    public void TheGuideRunsThroughTheHubOfASimultaneousGroup()
    {
        NoteGuides.Layout layout = NoteGuides.Build(new[]
        {
            Note(0, 0, -100),
            Note(100, -50, 0),
            Note(100, 50, 0),
            Note(200, 0, 100),
        }, Radius);

        Assert.That(layout.Guides, Has.Count.EqualTo(2));
        Assert.That(layout.Guides[0].From, Is.EqualTo(new Vector2(0, -90)).Using<Vector2>(Close));
        Assert.That(layout.Guides[0].To, Is.EqualTo(new Vector2(0, -Hub)).Using<Vector2>(Close));
        Assert.That(layout.Guides[1].From, Is.EqualTo(new Vector2(0, Hub)).Using<Vector2>(Close));
        Assert.That(layout.Guides[1].To, Is.EqualTo(new Vector2(0, 90)).Using<Vector2>(Close));
    }

    [Test]
    public void ThreeChordNotesShareOneHubAtTheirCentre()
    {
        NoteGuides.Layout layout = NoteGuides.Build(new[]
        {
            Note(100, 0, 0), Note(100, 90, 0), Note(100, 0, 90),
        }, Radius);

        Assert.That(layout.Hubs, Has.Count.EqualTo(1));
        Assert.That(layout.Hubs[0].Center, Is.EqualTo(new Vector2(30, 30)).Using<Vector2>(Close));
        Assert.That(layout.Spokes, Has.Count.EqualTo(3));
    }

    [Test]
    public void OverlappingNotesDrawNoLineBetweenThem()
    {
        NoteGuides.Layout layout = NoteGuides.Build(new[] { Note(0, 0, 0), Note(100, 15, 0) }, Radius);

        Assert.That(layout.Guides, Is.Empty);
    }

    [Test]
    public void AGuideIsAsFaintAsItsFainterEnd()
    {
        NoteGuides.Layout layout = NoteGuides.Build(new[] { Note(0, 0, 0, 0.9f), Note(100, 100, 0, 0.3f) }, Radius);

        Assert.That(layout.Guides[0].Alpha, Is.EqualTo(0.3f));
    }
}

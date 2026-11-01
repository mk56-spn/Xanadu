using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Playfields;

namespace Xanadu.Tests.Playfields;

[TestFixture]
public sealed class NoteReachTests
{
    private static PlayfieldView View(float scale) =>
        new(Vector2.Zero, scale, new Vector2(1_000, 1_000));

    [Test]
    public void NoteInsideTheSphereIsInReach()
    {
        Assert.That(
            NoteReach.Contains(
                new Vector2(500, 500),
                View(1f),
                new Vector2(599, 500)),
            Is.True);
        Assert.That(
            NoteReach.Contains(
                new Vector2(500, 500),
                View(1f),
                new Vector2(500, 401)),
            Is.True);
    }

    [Test]
    public void NoteBeyondTheSphereIsOutOfReach()
    {
        Assert.That(
            NoteReach.Contains(
                new Vector2(500, 500),
                View(1f),
                new Vector2(601, 500)),
            Is.False);
        Assert.That(
            NoteReach.Contains(
                new Vector2(500, 500),
                View(1f),
                new Vector2(500, 399)),
            Is.False);
    }

    [Test]
    public void ContactReachesTheEdgeOfTheNoteCircle()
    {
        Assert.That(
            NoteReach.Contains(
                new Vector2(500, 500),
                View(1f),
                new Vector2(635, 500),
                CharacterKind.Contact),
            Is.True);
        Assert.That(
            NoteReach.Contains(
                new Vector2(500, 500),
                View(1f),
                new Vector2(637, 500),
                CharacterKind.Contact),
            Is.False);
    }

    [Test]
    public void ContactEdgeReachScalesWithThePlayfield()
    {
        Assert.That(
            NoteReach.Contains(
                new Vector2(500, 500),
                View(0.5f),
                new Vector2(567, 500),
                CharacterKind.Contact),
            Is.True);
        Assert.That(
            NoteReach.Contains(
                new Vector2(500, 500),
                View(0.5f),
                new Vector2(569, 500),
                CharacterKind.Contact),
            Is.False);
    }

    [Test]
    public void OtherCharactersStillHaveToReachTheCenter()
    {
        Assert.That(
            NoteReach.Contains(
                new Vector2(500, 500),
                View(1f),
                new Vector2(601, 500),
                CharacterKind.Armed),
            Is.False);
    }

    [Test]
    public void ReachIsFarWiderThanThePlayerBox()
    {
        Assert.That(NoteReach.Radius, Is.GreaterThan(Player.DefaultWidth * 2));
        Assert.That(NoteReach.Radius, Is.EqualTo(100f));
    }

    [Test]
    public void ReachScalesWithThePlayfield()
    {
        Assert.That(NoteReach.OnScreen(0.5f), Is.EqualTo(50f));
        Assert.That(
            NoteReach.Contains(
                new Vector2(500, 500),
                View(0.5f),
                new Vector2(540, 500)),
            Is.True);
        Assert.That(
            NoteReach.Contains(
                new Vector2(500, 500),
                View(0.5f),
                new Vector2(560, 500)),
            Is.False);
    }
}

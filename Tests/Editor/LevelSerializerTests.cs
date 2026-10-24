using NUnit.Framework;
using Writer.Levels;
using Writer.Editor;
using Writer.Ecs;
using Writer.Settings;

namespace Writer.Tests.Editor;

[TestFixture]
public sealed class LevelSerializerTests
{
    [Test]
    public void LevelIdentityAndSongReferenceRoundTrip()
    {
        var source = new LevelDocument
        {
            Id = "level-1",
            Slug = "hard-level",
            SongId = "song-1",
            Name = "Hard",
        };

        LevelDocument result = LevelSerializer.Deserialize(
            LevelSerializer.Serialize(source));

        Assert.That(result.Id, Is.EqualTo("level-1"));
        Assert.That(result.Slug, Is.EqualTo("hard-level"));
        Assert.That(result.SongId, Is.EqualTo("song-1"));
    }

    [Test]
    public void NullNotesDeserializeToEmptyList()
    {
        LevelDocument document = LevelSerializer.Deserialize(
            """{"Name":"Null Notes","Notes":null}""");

        Assert.That(document.Notes, Is.Not.Null);
        Assert.That(document.Notes, Is.Empty);
        Assert.That(document.Mines, Is.Not.Null);
        Assert.That(document.Mines, Is.Empty);
        Assert.That(document.Version, Is.EqualTo(2));
        Assert.DoesNotThrow(() => LevelValidator.Validate(document));
    }

    [Test]
    public void NotesDoNotSerializeFadeFields()
    {
        var source = new LevelDocument();
        source.Notes.Add(new NoteDocument
        {
            TimeMs = 1_000,
            X = 100,
            Y = 200,
        });

        string json = LevelSerializer.Serialize(source);

        Assert.That(json, Does.Not.Contain("Fade"));
    }

    [Test]
    public void RoundTripsLevelAndNotes()
    {
        var source = new LevelDocument
        {
            Name = "Test Level",
            SnapDivision = 8,
            PlayfieldWidth = 1000,
            PlayfieldHeight = 1000,
        };
        source.Notes.Add(new NoteDocument
        {
            TimeMs = 1_250,
            X = 250,
            Y = 400,
            Target = KeybindAction.R2,
            Kind = NoteKind.Movement,
            DashDirection = DashDirection.UpRight,
        });
        source.Mines.Add(new MineDocument
        {
            TimeMs = 1_500,
            X = 500,
            Y = 600,
        });

        string json = LevelSerializer.Serialize(source);
        LevelDocument result = LevelSerializer.Deserialize(json);

        Assert.That(result.Version, Is.EqualTo(2));
        Assert.That(result.Name, Is.EqualTo("Test Level"));
        Assert.That(result.SnapDivision, Is.EqualTo(8));
        Assert.That(result.PlayfieldWidth, Is.EqualTo(1000));
        Assert.That(result.PlayfieldHeight, Is.EqualTo(1000));
        Assert.That(result.Notes, Has.Count.EqualTo(1));
        Assert.That(result.Notes[0].TimeMs, Is.EqualTo(1_250));
        Assert.That(result.Notes[0].X, Is.EqualTo(250));
        Assert.That(result.Notes[0].Y, Is.EqualTo(400));
        Assert.That(result.Notes[0].Target, Is.EqualTo(KeybindAction.R2));
        Assert.That(result.Notes[0].Kind, Is.EqualTo(NoteKind.Movement));
        Assert.That(
            result.Notes[0].DashDirection,
            Is.EqualTo(DashDirection.UpRight));
        Assert.That(result.Mines, Has.Count.EqualTo(1));
        Assert.That(result.Mines[0].TimeMs, Is.EqualTo(1_500));
        Assert.That(result.Mines[0].X, Is.EqualTo(500));
    }
}

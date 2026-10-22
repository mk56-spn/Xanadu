using NUnit.Framework;
using Writer.Levels;
using Writer.Editor;
using Writer.Settings;

namespace Writer.Tests.Editor;

[TestFixture]
public sealed class LevelSerializerTests
{
    [Test]
    public void NullNotesDeserializeToEmptyList()
    {
        LevelDocument document = LevelSerializer.Deserialize(
            """{"Name":"Null Notes","Notes":null}""");

        Assert.That(document.Notes, Is.Not.Null);
        Assert.That(document.Notes, Is.Empty);
        Assert.That(document.MovementEvents, Is.Not.Null);
        Assert.That(document.MovementEvents, Is.Empty);
        Assert.That(document.Mines, Is.Not.Null);
        Assert.That(document.Mines, Is.Empty);
        Assert.That(document.Version, Is.EqualTo(1));
        Assert.DoesNotThrow(() => LevelValidator.Validate(document));
    }

    [Test]
    public void RoundTripsLevelAndNotes()
    {
        var source = new LevelDocument
        {
            Name = "Test Level",
            AudioPath = "res://Audio/test-track.wav",
            Artist = "Test Artist",
            Difficulty = "Hard",
            Bpm = 174,
            OffsetMs = -35,
            SnapDivision = 8,
            PlayfieldWidth = 1000,
            PlayfieldHeight = 1000,
        };
        source.Notes.Add(new NoteDocument
        {
            TimeMs = 1_250,
            X = 250,
            Y = 400,
            FadeInMs = 600,
            FadeOutMs = 600,
            Target = KeybindAction.R2,
        });
        source.MovementEvents.Add(new MovementEventDocument { TimeMs = 800 });
        source.Mines.Add(new MineDocument
        {
            TimeMs = 1_500,
            X = 500,
            Y = 600,
        });

        string json = LevelSerializer.Serialize(source);
        LevelDocument result = LevelSerializer.Deserialize(json);

        Assert.That(result.Version, Is.EqualTo(1));
        Assert.That(result.Name, Is.EqualTo("Test Level"));
        Assert.That(result.AudioPath, Is.EqualTo("res://Audio/test-track.wav"));
        Assert.That(result.Artist, Is.EqualTo("Test Artist"));
        Assert.That(result.Difficulty, Is.EqualTo("Hard"));
        Assert.That(result.Bpm, Is.EqualTo(174));
        Assert.That(result.OffsetMs, Is.EqualTo(-35));
        Assert.That(result.SnapDivision, Is.EqualTo(8));
        Assert.That(result.PlayfieldWidth, Is.EqualTo(1000));
        Assert.That(result.PlayfieldHeight, Is.EqualTo(1000));
        Assert.That(result.Notes, Has.Count.EqualTo(1));
        Assert.That(result.Notes[0].TimeMs, Is.EqualTo(1_250));
        Assert.That(result.Notes[0].X, Is.EqualTo(250));
        Assert.That(result.Notes[0].Y, Is.EqualTo(400));
        Assert.That(result.Notes[0].Target, Is.EqualTo(KeybindAction.R2));
        Assert.That(result.MovementEvents, Has.Count.EqualTo(1));
        Assert.That(result.MovementEvents[0].TimeMs, Is.EqualTo(800));
        Assert.That(result.Mines, Has.Count.EqualTo(1));
        Assert.That(result.Mines[0].TimeMs, Is.EqualTo(1_500));
        Assert.That(result.Mines[0].X, Is.EqualTo(500));
    }
}

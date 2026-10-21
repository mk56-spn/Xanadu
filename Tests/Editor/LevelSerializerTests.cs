using NUnit.Framework;
using Writer.Editor;

namespace Writer.Tests.Editor;

[TestFixture]
public sealed class LevelSerializerTests
{
    [Test]
    public void RoundTripsLevelAndNotes()
    {
        var source = new LevelDocument
        {
            Name = "Test Level",
            AudioPath = "res://Audio/test-track.wav",
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
        });

        string json = LevelSerializer.Serialize(source);
        LevelDocument result = LevelSerializer.Deserialize(json);

        Assert.That(result.Name, Is.EqualTo("Test Level"));
        Assert.That(result.AudioPath, Is.EqualTo("res://Audio/test-track.wav"));
        Assert.That(result.Bpm, Is.EqualTo(174));
        Assert.That(result.OffsetMs, Is.EqualTo(-35));
        Assert.That(result.SnapDivision, Is.EqualTo(8));
        Assert.That(result.PlayfieldWidth, Is.EqualTo(1000));
        Assert.That(result.PlayfieldHeight, Is.EqualTo(1000));
        Assert.That(result.Notes, Has.Count.EqualTo(1));
        Assert.That(result.Notes[0].TimeMs, Is.EqualTo(1_250));
        Assert.That(result.Notes[0].X, Is.EqualTo(250));
        Assert.That(result.Notes[0].Y, Is.EqualTo(400));
    }
}

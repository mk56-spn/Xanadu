using System.Collections.Generic;
using NUnit.Framework;
using Writer.Levels;
using Writer.Editor;

namespace Writer.Tests.Editor;

[TestFixture]
public sealed class LevelValidatorTests
{
    [Test]
    public void ValidLevelHasNoIssues()
    {
        LevelDocument document = ValidDocument();

        Assert.That(LevelValidator.Validate(document), Is.Empty);
    }

    [Test]
    public void MissingSongAndNameAreReported()
    {
        LevelDocument document = ValidDocument();
        document.Name = "";
        document.SongId = "";

        List<string> issues = LevelValidator.Validate(document);

        Assert.That(issues, Has.Some.Contains("name"));
        Assert.That(issues, Has.Some.Contains("song"));
    }

    [Test]
    public void EmptyChartIsRejected()
    {
        LevelDocument document = ValidDocument();
        document.Notes.Clear();

        Assert.That(LevelValidator.Validate(document), Has.Some.Contains("note"));
    }

    [Test]
    public void OutOfBoundsNoteIsRejected()
    {
        LevelDocument document = ValidDocument();
        document.Notes[0].X = 1200;

        Assert.That(LevelValidator.Validate(document), Has.Some.Contains("outside"));
    }

    private static LevelDocument ValidDocument()
    {
        var document = new LevelDocument
        {
            Name = "Valid",
            SongId = "song",
            PlayfieldWidth = 1000,
            PlayfieldHeight = 1000,
        };
        document.Notes.Add(new NoteDocument
        {
            TimeMs = 500,
            X = 500,
            Y = 500,
        });
        return document;
    }
}

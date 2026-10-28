using System.Collections.Generic;
using NUnit.Framework;
using Xanadu.Levels;
using Xanadu.Editor;

namespace Xanadu.Tests.Editor;

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

    [Test]
    public void OutOfBoundsBlockIsRejected()
    {
        LevelDocument document = ValidDocument();
        document.Blocks.Add(new BlockDocument
        {
            X = 900,
            Y = 0,
            Width = 200,
            Height = 100,
        });

        Assert.That(
            LevelValidator.Validate(document),
            Has.Some.Contains("outside"));
    }

    [Test]
    public void BlockWithoutSizeIsRejected()
    {
        LevelDocument document = ValidDocument();
        document.Blocks.Add(new BlockDocument { X = 0, Y = 0 });

        Assert.That(
            LevelValidator.Validate(document),
            Has.Some.Contains("no size"));
    }

    [Test]
    public void ValidSpikesAreAccepted()
    {
        LevelDocument document = ValidDocument();
        document.Spikes.Add(new SpikeDocument
        {
            X = 900,
            Y = 900,
            Width = 100,
            Height = 100,
        });

        Assert.That(LevelValidator.Validate(document), Is.Empty);
    }

    [Test]
    public void OutOfBoundsSpikeIsRejected()
    {
        LevelDocument document = ValidDocument();
        document.Spikes.Add(new SpikeDocument
        {
            X = 950,
            Y = 0,
            Width = 100,
            Height = 100,
        });

        Assert.That(
            LevelValidator.Validate(document),
            Has.Some.Contains("Spike 1 is outside"));
    }

    [Test]
    public void SpikeWithoutSizeIsRejected()
    {
        LevelDocument document = ValidDocument();
        document.Spikes.Add(new SpikeDocument { X = 0, Y = 0 });

        Assert.That(
            LevelValidator.Validate(document),
            Has.Some.Contains("Spike 1 has no size"));
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

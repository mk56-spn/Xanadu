using System.Linq;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Levels;
using Xanadu.Settings;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class LevelSerializerTests
{
    [Test]
    public void MapsAreXmfFiles()
    {
        byte[] map = LevelSerializer.Serialize(new LevelDocument());

        Assert.That(map.Take(4), Is.EqualTo("XMF1"u8.ToArray()));
    }

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
    public void NormalizeFillsMissingLists()
    {
        var document = new LevelDocument
        {
            Notes = null!,
            Mines = null!,
            Blocks = null!,
            Spikes = null!,
            StartPoints = null!,
            Groups = null!,
            Shaders = null!,
        };

        LevelDocument result = LevelSerializer.Normalize(document);

        Assert.That(result.Notes, Is.Not.Null);
        Assert.That(result.Notes, Is.Empty);
        Assert.That(result.Mines, Is.Not.Null);
        Assert.That(result.Mines, Is.Empty);
        Assert.That(result.Blocks, Is.Not.Null);
        Assert.That(result.Blocks, Is.Empty);
        Assert.That(result.Spikes, Is.Not.Null);
        Assert.That(result.Spikes, Is.Empty);
        Assert.That(result.StartPoints, Is.Not.Null);
        Assert.That(result.Groups, Is.Not.Null);
        Assert.That(result.Shaders, Is.Not.Null);
        Assert.That(result.Version, Is.EqualTo(2));
        Assert.DoesNotThrow(() => LevelValidator.Validate(result));
    }

    [Test]
    public void BlocksRoundTrip()
    {
        var source = new LevelDocument();
        source.Blocks.Add(new BlockDocument
        {
            X = 100,
            Y = 200,
            Width = 300,
            Height = 40,
        });

        LevelDocument result = LevelSerializer.Deserialize(
            LevelSerializer.Serialize(source));

        Assert.That(result.Blocks, Has.Count.EqualTo(1));
        Assert.That(result.Blocks[0].X, Is.EqualTo(100));
        Assert.That(result.Blocks[0].Y, Is.EqualTo(200));
        Assert.That(result.Blocks[0].Width, Is.EqualTo(300));
        Assert.That(result.Blocks[0].Height, Is.EqualTo(40));
    }

    [Test]
    public void SpikesRoundTrip()
    {
        var source = new LevelDocument();
        source.Spikes.Add(new SpikeDocument
        {
            X = 200,
            Y = 800,
            Width = 100,
            Height = 100,
            Groups = new System.Collections.Generic.List<int> { 3 },
        });

        LevelDocument result = LevelSerializer.Deserialize(
            LevelSerializer.Serialize(source));

        Assert.That(result.Spikes, Has.Count.EqualTo(1));
        Assert.That(result.Spikes[0].X, Is.EqualTo(200));
        Assert.That(result.Spikes[0].Y, Is.EqualTo(800));
        Assert.That(result.Spikes[0].Width, Is.EqualTo(100));
        Assert.That(result.Spikes[0].Height, Is.EqualTo(100));
        Assert.That(result.Spikes[0].Groups, Is.EqualTo(new[] { 3 }));
    }

    [Test]
    public void ALevelWithoutSpikesStillLoads()
    {
        // The spike chunk is optional, so files written before it existed load
        // with no spikes rather than failing.
        LevelDocument result = LevelSerializer.Deserialize(
            LevelSerializer.Serialize(new LevelDocument()));

        Assert.That(result.Spikes, Is.Not.Null);
        Assert.That(result.Spikes, Is.Empty);
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

        LevelDocument result = LevelSerializer.Deserialize(
            LevelSerializer.Serialize(source));

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

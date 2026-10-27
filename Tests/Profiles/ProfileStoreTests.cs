using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Xanadu.Profiles;
using Xanadu.Scenes;

namespace Xanadu.Tests.Profiles;

[TestFixture]
public sealed class ProfileStoreTests
{
    [Test]
    public void CreatesSelectsAndPersistsProfilesAndScores()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "writer-profiles-" + Guid.NewGuid().ToString("N") + ".json");

        try
        {
            var store = new ProfileStore(path);
            UserProfile first = store.Create("Alice");
            UserProfile second = store.Create("Bob");
            Assert.That(store.ActiveProfileId, Is.EqualTo(second.Id));
            Assert.That(store.Select(first.Id), Is.True);
            Assert.That(store.ActiveProfile?.Name, Is.EqualTo("Alice"));

            string level = Path.Combine(Path.GetTempPath(), "level.json");
            store.RecordScore(level, new PlayResults("Level", 900_000, 0.97, 42, 50, 55));
            store.RecordScore(level, new PlayResults("Level", 800_000, 0.90, 20, 40, 55));

            Assert.That(store.Best(level)?.Score, Is.EqualTo(900_000));

            var reloaded = new ProfileStore(path);
            Assert.That(reloaded.Profiles, Has.Count.EqualTo(2));
            Assert.That(reloaded.ActiveProfile?.Name, Is.EqualTo("Alice"));
            Assert.That(reloaded.Best(level)?.MaxCombo, Is.EqualTo(42));
        }
        finally
        {
            File.Delete(path);
        }
    }


    [Test]
    public void AllScoresHandlesOverflowSizedLists()
    {
        var store = new ProfileStore(persist: false);
        string level = Path.Combine(Path.GetTempPath(), "level.json");
        for (int i = 0; i < 30; i++)
        {
            store.Create($"Player {i}");
            store.RecordScore(
                level,
                new PlayResults("Level", 100_000 + i, 0.5, i, 30, 30));
        }

        IReadOnlyList<ScoreEntry> scores = store.AllScores(level);
        Assert.That(scores, Has.Count.EqualTo(30));
        Assert.That(scores[0].Record.Score, Is.EqualTo(100_029));
        Assert.That(scores[^1].Record.Score, Is.EqualTo(100_000));
    }

    [Test]
    public void AllScoresAreSortedAcrossProfiles()
    {
        var store = new ProfileStore(persist: false);
        UserProfile alice = store.Create("Alice");
        string level = Path.Combine(Path.GetTempPath(), "level.json");
        store.RecordScore(level, new PlayResults("Level", 700_000, 0.7, 10, 20, 30));

        UserProfile bob = store.Create("Bob");
        store.RecordScore(level, new PlayResults("Level", 900_000, 0.9, 30, 30, 30));

        IReadOnlyList<ScoreEntry> scores = store.AllScores(level);
        Assert.That(scores, Has.Count.EqualTo(2));
        Assert.That(scores[0].ProfileName, Is.EqualTo("Bob"));
        Assert.That(scores[0].Record.Score, Is.EqualTo(900_000));
        Assert.That(scores[1].ProfileName, Is.EqualTo("Alice"));
    }
}

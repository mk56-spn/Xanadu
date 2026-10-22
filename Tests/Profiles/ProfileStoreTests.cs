using System;
using System.IO;
using NUnit.Framework;
using Writer.Profiles;
using Writer.Scenes;

namespace Writer.Tests.Profiles;

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
}

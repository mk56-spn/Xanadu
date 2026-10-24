using System;
using System.IO;
using NUnit.Framework;
using Writer.Levels;

namespace Writer.Tests.Editor;

[TestFixture]
public sealed class LevelStoreTests
{
    [Test]
    public void DeleteRemovesTheLevelFile()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "writer-level-tests-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "delete.json");
        try
        {
            LevelStore.Save(new LevelDocument { Name = "Delete" }, path);
            Assert.That(File.Exists(path), Is.True);

            LevelStore.Delete(path);

            Assert.That(File.Exists(path), Is.False);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void NextUnnamedPathUsesTheFirstFreeNumber()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "writer-level-tests-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "unnamed-1.json"), "{}");
            File.WriteAllText(Path.Combine(directory, "unnamed-2.json"), "{}");

            Assert.That(
                LevelStore.NextUnnamedPath(directory),
                Is.EqualTo(Path.Combine(directory, "unnamed-3.json")));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void SaveThenLoadRoundTrips()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "writer-level-tests-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "level.json");

        try
        {
            var document = new LevelDocument
            {
                Name = "Store Test",
            };
            document.Notes.Add(new NoteDocument
            {
                TimeMs = 500,
                X = 100,
                Y = 200,
            });

            LevelStore.Save(document, path);
            LevelDocument loaded = LevelStore.Load(path);

            Assert.That(File.Exists(path), Is.True);
            Assert.That(loaded.Name, Is.EqualTo("Store Test"));
            Assert.That(loaded.Notes, Has.Count.EqualTo(1));
            Assert.That(loaded.Notes[0].TimeMs, Is.EqualTo(500));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}

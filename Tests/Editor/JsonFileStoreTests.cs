using System.IO;
using NUnit.Framework;
using Writer.Ecs.Storage;

namespace Writer.Tests.Editor;

[TestFixture]
public sealed class JsonFileStoreTests
{
    [Test]
    public void SaveAndLoadRoundTripWithoutLeavingTemporaryFile()
    {
        string directory = Path.Combine(
            Path.GetTempPath(), "writer-json-" + System.Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "value.json");
        try
        {
            JsonFileStore.Save(path, new Payload("hello"));
            Payload? loaded = JsonFileStore.Load<Payload>(path);

            Assert.That(loaded?.Value, Is.EqualTo("hello"));
            Assert.That(File.Exists(path + ".tmp"), Is.False);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void LoadAllSkipsMalformedFiles()
    {
        string directory = Path.Combine(
            Path.GetTempPath(), "writer-json-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            JsonFileStore.Save(Path.Combine(directory, "valid.json"), new Payload("ok"));
            File.WriteAllText(Path.Combine(directory, "broken.json"), "{ nope");

            Assert.That(
                JsonFileStore.LoadAll<Payload>(directory),
                Has.Exactly(1).Matches<Payload>(item => item.Value == "ok"));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private sealed record Payload(string Value);
}

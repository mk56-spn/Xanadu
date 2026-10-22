using System.Text.Json;
using Writer.Ecs.Storage;

namespace Writer.Levels;

/// <summary>JSON serialization for level documents.</summary>
public static class LevelSerializer
{
    private static JsonSerializerOptions Options => JsonFileStore.Options;

    public static string Serialize(LevelDocument document) =>
        JsonSerializer.Serialize(document, Options);

    public static LevelDocument Deserialize(string json)
    {
        LevelDocument document =
            JsonSerializer.Deserialize<LevelDocument>(json, Options) ?? new LevelDocument();
        return Normalize(document);
    }

    public static LevelDocument Normalize(LevelDocument document)
    {
        if (document.Version <= 0)
            document.Version = 1;
        if (string.IsNullOrWhiteSpace(document.Id))
            document.Id = System.Guid.NewGuid().ToString("N");
        if (string.IsNullOrWhiteSpace(document.Slug))
            document.Slug = "level-" + document.Id[..8];

        document.Notes ??= new();
        document.Notes.RemoveAll(note => note is null);
        document.Mines ??= new();
        document.StartPoints ??= new();
        document.Mines.RemoveAll(mine => mine is null);
        return document;
    }
}

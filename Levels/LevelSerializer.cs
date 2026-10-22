using System.Text.Json;

namespace Writer.Levels;

/// <summary>JSON serialization for level documents.</summary>
public static class LevelSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public static string Serialize(LevelDocument document) =>
        JsonSerializer.Serialize(document, Options);

    public static LevelDocument Deserialize(string json)
    {
        LevelDocument document =
            JsonSerializer.Deserialize<LevelDocument>(json, Options) ?? new LevelDocument();

        if (document.Version <= 0)
            document.Version = 1;

        document.Notes ??= new();
        document.Notes.RemoveAll(note => note is null);
        document.Mines ??= new();
        document.StartPoints ??= new();
        document.Mines.RemoveAll(mine => mine is null);
        return document;
    }
}

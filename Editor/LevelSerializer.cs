using System.Text.Json;

namespace Writer.Editor;

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

    public static LevelDocument Deserialize(string json) =>
        JsonSerializer.Deserialize<LevelDocument>(json, Options) ?? new LevelDocument();
}

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
        document.StartPoints.RemoveAll(start => start is null);
        document.Blocks ??= new();
        document.Groups ??= new();
        document.Groups.RemoveAll(group => group is null || group.Id is < 0 or >= Writer.Ecs.GroupLimits.Count);
        foreach (GroupDocument group in document.Groups)
        {
            group.Name ??= "";
            group.Effects ??= new();
            group.Effects.RemoveAll(effect => effect is null);
            foreach (GroupEffectDocument effect in group.Effects)
            {
                if (!System.Enum.IsDefined(effect.Ease))
                    effect.Ease = Writer.Ecs.GroupEase.Linear;
            }
        }

        foreach (NoteDocument note in document.Notes)
            note.Groups ??= new();
        foreach (StartPointDocument start in document.StartPoints)
            start.Groups ??= new();
        document.Mines.RemoveAll(mine => mine is null);
        document.Blocks.RemoveAll(block => block is null);
        foreach (MineDocument mine in document.Mines)
            mine.Groups ??= new();
        foreach (BlockDocument block in document.Blocks)
            block.Groups ??= new();
        return document;
    }
}

using System.Collections.Generic;
using Writer.Levels;

namespace Writer.Editor;

/// <summary>Validates a level before saving or previewing.</summary>
public static class LevelValidator
{
    public static List<string> Validate(LevelDocument document)
    {
        var issues = new List<string>();

        if (string.IsNullOrWhiteSpace(document.Name))
            issues.Add("Level name is required.");

        if (string.IsNullOrWhiteSpace(document.SongId))
            issues.Add("Assign a song.");

        if (document.PlayfieldWidth <= 0 || document.PlayfieldHeight <= 0)
            issues.Add("Playfield size must be positive.");

        if (document.Notes.Count == 0)
            issues.Add("Add at least one note.");

        for (int i = 0; i < document.Notes.Count; i++)
        {
            NoteDocument note = document.Notes[i];

            if (note.TimeMs < 0)
                issues.Add($"Note {i + 1} has a negative time.");

            if (note.X < 0 ||
                note.Y < 0 ||
                note.X > document.PlayfieldWidth ||
                note.Y > document.PlayfieldHeight)
            {
                issues.Add($"Note {i + 1} is outside the playfield.");
            }
        }

        return issues;
    }
}

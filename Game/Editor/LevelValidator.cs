using System.Collections.Generic;
using Xanadu.Levels;

namespace Xanadu.Editor;

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

        for (int i = 0; i < document.Blocks.Count; i++)
        {
            BlockDocument block = document.Blocks[i];

            if (block.Width <= 0 || block.Height <= 0)
                issues.Add($"Block {i + 1} has no size.");
            else if (block.X < 0 ||
                     block.Y < 0 ||
                     block.X + block.Width > document.PlayfieldWidth ||
                     block.Y + block.Height > document.PlayfieldHeight)
            {
                issues.Add($"Block {i + 1} is outside the playfield.");
            }
        }

        for (int i = 0; i < document.Spikes.Count; i++)
        {
            SpikeDocument spike = document.Spikes[i];

            if (spike.Width <= 0 || spike.Height <= 0)
                issues.Add($"Spike {i + 1} has no size.");
            else if (spike.X < 0 ||
                     spike.Y < 0 ||
                     spike.X + spike.Width > document.PlayfieldWidth ||
                     spike.Y + spike.Height > document.PlayfieldHeight)
            {
                issues.Add($"Spike {i + 1} is outside the playfield.");
            }
        }

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

        foreach (GroupDocument group in document.Groups)
        {
            string name = group.Name.Length > 0 ? group.Name : $"Group {group.Id + 1}";
            for (int i = 0; i < group.Effects.Count; i++)
            {
                GroupEffectDocument effect = group.Effects[i];
                if (effect.Start < 0)
                    issues.Add($"{name} effect {i + 1} starts before 0.");
                if (effect.End < effect.Start)
                    issues.Add($"{name} effect {i + 1} ends before it starts.");
            }
        }

        return issues;
    }
}

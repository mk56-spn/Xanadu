using System;
using System.IO;
using System.Linq;

namespace Writer.Misc;

/// <summary>Builds readable, collision-free screenshot file names.</summary>
public static class ScreenshotNaming
{
    public static string Create(
        string gameName,
        DateTime timestamp,
        Func<string, bool> exists)
    {
        string safeName = Sanitize(
            string.IsNullOrWhiteSpace(gameName) ? "Writer" : gameName);
        string baseName = $"{safeName} {timestamp:yyyy-MM-dd HH-mm-ss}";
        string file = $"{baseName}.png";
        int suffix = 2;

        while (exists(file))
        {
            file = $"{baseName} ({suffix}).png";
            suffix++;
        }

        return file;
    }

    private static string Sanitize(string name)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string safe = new(name
            .Select(character => invalid.Contains(character) ? '-' : character)
            .ToArray());

        return safe.Trim().TrimEnd('.');
    }
}

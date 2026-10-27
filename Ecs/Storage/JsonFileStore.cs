using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Xanadu.Ecs.Storage;

/// <summary>Shared atomic JSON persistence for program-owned data.</summary>
public static class JsonFileStore
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public static T? Load<T>(string path) where T : class
    {
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, Options);
    }

    public static void Save<T>(string path, T value)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        string temporary = path + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(value, Options));
            File.Move(temporary, path, overwrite: true);
        }
        catch
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
            throw;
        }
    }

    public static IEnumerable<T> LoadAll<T>(
        string directory,
        string pattern = "*.json") where T : class
    {
        if (!Directory.Exists(directory))
            yield break;

        foreach (string path in Directory.EnumerateFiles(
                     directory, pattern, SearchOption.AllDirectories))
        {
            T? value = null;
            try
            {
                value = Load<T>(path);
            }
            catch (Exception)
            {
                // Ignore malformed files; the caller still gets valid entries.
            }

            if (value is not null)
                yield return value;
        }
    }
}

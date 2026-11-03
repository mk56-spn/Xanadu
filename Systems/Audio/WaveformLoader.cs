using System.Collections.Generic;
using System.IO;
using Godot;

namespace Xanadu.Systems.Audio;

/// <summary>Decodes and analyses an audio file for the editor's timeline, once per file.</summary>
public static class WaveformLoader
{
    /// <summary>Enough for the highs band, and half the work of decoding at full rate.</summary>
    private const int SampleRate = 22050;

    private const int MaxCached = 12;

    private static readonly Dictionary<string, WaveformData> Cache = new();

    public static WaveformData? Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        string filePath = path.StartsWith("res://") || path.StartsWith("user://")
            ? ProjectSettings.GlobalizePath(path)
            : path;
        if (!File.Exists(filePath))
            return null;

        if (Cache.TryGetValue(filePath, out WaveformData? cached))
            return cached;

        float[]? samples = AudioDecoder.DecodeMono(filePath, SampleRate);
        if (samples is null)
            return null;

        WaveformData data = WaveformAnalyzer.Analyze(samples, SampleRate);
        if (Cache.Count >= MaxCached)
            Cache.Clear();

        Cache[filePath] = data;
        return data;
    }
}

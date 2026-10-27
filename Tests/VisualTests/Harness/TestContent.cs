using Xanadu.Levels;

namespace Xanadu.Tests.VisualTests;

/// <summary>Shared fixtures for visual scenes that need a song and chart.</summary>
public static class TestContent
{
    public static SongDocument Song(
        string id,
        string title,
        string audioPath = "res://Audio/test-track.wav",
        int bpm = 120)
    {
        return new SongDocument
        {
            Id = id,
            Slug = id,
            Title = title,
            AudioPath = audioPath,
            Bpm = bpm,
        };
    }

    public static LevelDocument Level(string name, SongDocument song)
    {
        return new LevelDocument
        {
            Name = name,
            SongId = song.Id,
            PlayfieldWidth = 1000,
            PlayfieldHeight = 1000,
        };
    }
}

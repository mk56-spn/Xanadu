using Xanadu.Levels;

namespace Xanadu.Tests.VisualTests;

/// <summary>Shared fixtures for visual scenes that need a song and chart.</summary>
public static partial class TestContent
{
    /// <summary>A song outside the library, playing a file of the test's choosing.</summary>
    public static LoadedSong Song(
        string id,
        string title,
        string audioPath = "Assets/Audio/test-track.wav",
        int bpm = 120)
    {
        return new LoadedSong(
            new SongDocument
            {
                Id = id,
                Slug = id,
                Title = title,
                Bpm = bpm,
            },
            audioPath);
    }

    public static LevelDocument Level(string name, LoadedSong song)
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

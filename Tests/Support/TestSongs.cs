using Xanadu.Levels;

namespace Xanadu.Tests.Support;

/// <summary>Songs outside any library, playing a file of the test's choosing.</summary>
public static class TestSongs
{
    public static LoadedSong At(string audioFile, string id = "test-song", string title = "Test Song") =>
        new(new SongDocument { Id = id, Slug = id, Title = title }, audioFile);
}

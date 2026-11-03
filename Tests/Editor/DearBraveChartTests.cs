using System;
using System.IO;
using System.Linq;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Ecs.Storage;
using Xanadu.Levels;
using Xanadu.Settings;
using Xanadu.Systems.Playfields;

namespace Xanadu.Tests.Editor;

/// <summary>
/// The timing check: one note on every quarter of a song whose timing is known good
/// from its osu! map, so a player can feel whether the game's clock is right.
/// </summary>
[TestFixture]
public sealed class DearBraveChartTests
{
    private static string Root => Path.GetFullPath(Path.Combine(
        TestContext.CurrentContext.TestDirectory,
        "../../../../"));

    private static LevelDocument Level() =>
        LevelStore.Load(Path.Combine(Root, "Levels", "dear-brave-quarter-notes.xmf"));

    private static SongDocument Song(LevelDocument level) =>
        JsonFileStore.Load<SongDocument[]>(Path.Combine(Root, "Songs", "catalog.json"))!
            .Single(song => song.Id == level.SongId);

    [Test]
    public void ThereIsANoteOnEveryQuarterAndNothingElse()
    {
        LevelDocument level = Level();
        SongDocument song = Song(level);
        double beat = 60_000d / song.Bpm;

        Assert.That(level.Notes[0].TimeMs, Is.EqualTo(song.OffsetMs));
        for (int i = 0; i < level.Notes.Count; i++)
        {
            Assert.That(
                level.Notes[i].TimeMs,
                Is.EqualTo(song.OffsetMs + i * beat).Within(0.51d),
                $"note {i}");
        }

        // The osu! map's last object is at 246564 ms.
        Assert.That(level.Notes[^1].TimeMs, Is.EqualTo(246_564).Within(beat));
    }

    [Test]
    public void EveryNoteIsOneKeyAndInReachOfWhereThePlayerStands()
    {
        LevelDocument level = Level();
        var view = new PlayfieldView(Vector2.Zero, 1f, new Vector2(1000, 1000));
        Vector2 standing = new(level.PlayfieldWidth / 2f, level.PlayfieldHeight - 50f);

        Assert.That(level.StartPoints, Is.Empty);
        foreach (NoteDocument note in level.Notes)
        {
            Assert.That(note.Kind, Is.EqualTo(NoteKind.Hit));
            Assert.That(note.Target, Is.EqualTo(KeybindAction.M));
            Assert.That(
                NoteReach.Contains(standing, view, new Vector2(note.X, note.Y), CharacterKind.Contact),
                Is.True);
        }
    }
}

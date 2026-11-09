using System;
using System.Linq;
using Xanadu.Levels;
using Xanadu.Settings;

namespace Xanadu.Host;

/// <summary>
/// <c>--demo</c>: a level made in memory, so the host can show notes, blocks and
/// a playing song without an editor yet. Notes sit on the ground from the
/// player's spawn (bottom centre), one a beat; nothing is saved.
/// </summary>
internal static class DemoLevel
{
    public static LevelEntry? Create()
    {
        LoadedSong? song = SongStore.Shared.Playable().FirstOrDefault();
        var document = new LevelDocument { Name = "Host demo", SongId = song?.Id ?? "" };
        float ground = document.PlayfieldHeight - 40f;
        float start = document.PlayfieldWidth / 2f;
        double beat = 60000.0 / (song?.Info.Bpm ?? 120);
        long first = (song?.Info.OffsetMs ?? 0) + (long)(beat * 4);
        for (int i = 0; i < 64; i++)
        {
            document.Notes.Add(new NoteDocument
            {
                TimeMs = first + (long)(beat * i),
                X = start + 160f + i * 140f,
                Y = ground,
                Target = (i % 3) switch
                {
                    0 => KeybindAction.Action1,
                    1 => KeybindAction.Action2,
                    _ => KeybindAction.Action3,
                },
            });
        }

        document.Blocks.Add(new BlockDocument { X = start + 300f, Y = ground - 260f, Width = 480f, Height = 40f });
        document.Blocks.Add(new BlockDocument { X = start - 900f, Y = ground - 420f, Width = 300f, Height = 300f });
        return LevelStore.Entry("", document);
    }
}

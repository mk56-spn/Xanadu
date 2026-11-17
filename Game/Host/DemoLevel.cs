using System.Collections.Generic;
using System.Linq;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Levels;
using Xanadu.Model.Playfields;
using Xanadu.Model.Settings;

namespace Xanadu.Host;

/// <summary>
/// <c>--demo</c>: a level made in memory, so the host can show curve notes and
/// a playing song without opening the editor. One curve note every bar, each a
/// wave across the field above the floor, cycling through the action keys;
/// nothing is saved.
/// </summary>
internal static class DemoLevel
{
    public static LevelEntry? Create()
    {
        LoadedSong? song = SongStore.Shared.Playable().FirstOrDefault();
        var document = new LevelDocument { Name = "Host demo", SongId = song?.Id ?? "" };
        double beat = 60000.0 / (song?.Info.Bpm ?? 120);
        long first = (song?.Info.OffsetMs ?? 0) + (long)(beat * 4);
        float floor = document.PlayfieldHeight - PlayRules.FloorDepth;
        float centre = document.PlayfieldWidth / 2f;
        for (int i = 0; i < 16; i++)
        {
            // A route that offers a turn every beat for a bar, ending on the next bar.
            document.Notes.Add(new NoteDocument
            {
                TimeMs = first + (long)(beat * 4 * i),
                X = centre - 300f,
                Y = floor - 400f,
                Route = [(long)beat, (long)(beat * 2), (long)(beat * 3), (long)(beat * 4)],
                RouteStrength = DashStrength.Single,
            });
        }

        return LevelStore.Entry("", LevelSerializer.Normalize(document));
    }
}

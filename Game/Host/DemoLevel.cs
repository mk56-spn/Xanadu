using System;
using Xanadu.Model.Settings;
using Xanadu.Serialisation.Levels;
using Xanadu.Storage.Levels;
using System.Linq;
using Xanadu.Model.Levels;
using Xanadu.Model.Playfields;

namespace Xanadu.Host;

/// <summary>
/// <c>--demo</c>: a level made in memory, so the host can show chords and
/// notes with a playing song without opening the editor. A chord every four
/// bars, moving along the field, and a note on every beat cycling through the
/// action keys, every fourth one a dash, after an entrance, with an orb hold
/// over bars 5 and 6, grass along the floor and a flare in the sky; nothing is saved. <c>XANADU_DEMO_EXIT</c> sets the
/// entrance's onset (fall, dash or orb).
/// </summary>
internal static class DemoLevel
{
    public static LevelEntry? Create()
    {
        LoadedSong? song = SongStore.Shared.Playable().FirstOrDefault();
        // No camera yet, so the field is what play shows at once.
        var document = new LevelDocument { Name = "Host demo", SongId = song?.Id ?? "", PlayfieldWidth = 3000, PlayfieldHeight = 1500 };
        double beat = 60000.0 / (song?.Info.Bpm ?? 120);
        long first = (song?.Info.OffsetMs ?? 0) + (long)(beat * 4);
        float floor = document.PlayfieldHeight - PlayRules.FloorDepth;
        for (int chord = 0; chord < 8; chord++)
        {
            document.Chords.Add(new ChordDocument
            {
                TimeMs = first + (long)(beat * 16 * chord),
                X = 500f + chord % 4 * 600f,
                Y = floor - 250f - chord % 3 * 150f,
            });
        }

        // The entrance in the middle of the field, a beat before the notes start.
        // XANADU_DEMO_EXIT picks its onset: fall (the default), dash (up) or orb.
        var entrance = new NoteDocument
        {
            TimeMs = first,
            X = document.PlayfieldWidth / 2f,
            Y = floor / 2f,
            Kind = Xanadu.Model.Ecs.Components.NoteKind.Entrance,
        };
        switch (System.Environment.GetEnvironmentVariable("XANADU_DEMO_EXIT"))
        {
            case "dash":
                entrance.Exit = Xanadu.Model.Ecs.Components.EntranceExit.Dash;
                entrance.Target = KeybindAction.Up;
                break;
            case "orb":
                entrance.Exit = Xanadu.Model.Ecs.Components.EntranceExit.Orb;
                entrance.EndMs = first + (long)(beat * 3);
                entrance.Path =
                [
                    new Xanadu.Model.Ecs.Components.OrbKey((long)(beat * 1.5), -250f),
                    new Xanadu.Model.Ecs.Components.OrbKey((long)(beat * 3), -100f),
                ];
                break;
        }

        document.Notes.Add(entrance);

        // A pad on the floor that fires on touch.
        document.Pads.Add(new PadDocument { X = 1300f, Y = floor - Pads.Size.Y / 2f, Speed = Pads.DefaultSpeed });

        // A row of updrafts just right of the entrance, where the body falls
        // after it comes out, holding it up off the floor.
        for (int i = 0; i < 3; i++)
            document.Drafts.Add(new DraftDocument { X = document.PlayfieldWidth / 2f + 150f + i * Drafts.Diameter, Y = floor - Drafts.EmitterHeight / 2f, Height = Drafts.DefaultHeight });

        // Grass along the floor, each patch its own scatter, and a flare up in the sky: decos, only for show.
        var grassPatches = new GroupDocument { Name = "grass" };
        for (int i = 0; i < 8; i++)
        {
            Xanadu.Core.Items.Xif grass = Decos.Grass();
            grass.Get<Xanadu.Core.Items.XifInstancer>()!.Seed = (uint)(i + 1);
            grass.Id = $"grass-{i + 1}";
            grassPatches.Members.Add(grass.Id);
            document.Decos.Add(new DecoDocument { X = 180f + i * 380f, Y = floor, Xif = grass });
        }
        Xanadu.Core.Items.Xif flare = Decos.Flare();
        flare.Id = "flare-1";
        document.Decos.Add(new DecoDocument { X = 2500f, Y = 260f, Xif = flare });

        // Groups over them, on the song's beats: the grass rocks a little over
        // two bars, and the flare swells and dims on every beat.
        Loop(grassPatches.Clip, 8f);
        Keys(grassPatches.Clip.Need(new Xanadu.Core.Keys.KeyAddress("", "", "x")), (0f, 0f), (4f, 40f), (8f, 0f));
        document.Groups.Add(grassPatches);
        var pulse = new GroupDocument { Name = "flare pulse", Members = { flare.Id } };
        Loop(pulse.Clip, 1f);
        Keys(pulse.Clip.Need(new Xanadu.Core.Keys.KeyAddress("", "", "scale")), (0f, 1.35f), (1f, 0.85f));
        Keys(pulse.Clip.Need(new Xanadu.Core.Keys.KeyAddress("", "", "opacity")), (0f, 1f), (1f, 0.4f));
        document.Groups.Add(pulse);

        // Bars 5 and 6 are one orb hold, rising and falling, with no taps under it.
        const int OrbFrom = 16, OrbTo = 23;
        document.Notes.Add(new NoteDocument
        {
            TimeMs = first + (long)(beat * OrbFrom),
            EndMs = first + (long)(beat * OrbTo),
            X = 400f,
            Y = floor - 500f,
            Kind = Xanadu.Model.Ecs.Components.NoteKind.Orb,
            Path =
            [
                new Xanadu.Model.Ecs.Components.OrbKey((long)(beat * 2), -300f),
                new Xanadu.Model.Ecs.Components.OrbKey((long)(beat * 4), 50f),
                new Xanadu.Model.Ecs.Components.OrbKey((long)(beat * 7), -350f),
            ],
        });

        // Three taps then a dash, every bar.
        for (int i = 1; i < 8 * 16; i++)
        {
            if (i >= OrbFrom && i <= OrbTo)
                continue;

            bool dash = i % 4 == 3;
            document.Notes.Add(new NoteDocument
            {
                TimeMs = first + (long)(beat * i),
                Target = KeybindActions.ActionKeys[i % KeybindActions.ActionKeys.Length],
                Kind = dash ? Xanadu.Model.Ecs.Components.NoteKind.Dash : Xanadu.Model.Ecs.Components.NoteKind.Tap,
            });
        }

        return LevelStore.Entry("", LevelSerializer.Normalize(document));
    }

    private static void Loop(Xanadu.Core.Keys.KeyClip clip, float beats)
    {
        clip.Duration = beats;
        clip.Loop = true;
        clip.LoopStart = 0f;
        clip.LoopEnd = beats;
    }

    /// <summary>Keys (beat, value) on a channel, eased in and out between them.</summary>
    private static void Keys(Xanadu.Core.Keys.KeyChannel channel, params (float Beat, float Value)[] keys)
    {
        foreach ((float beat, float value) in keys)
            channel.Keys.Add(new Xanadu.Core.Keys.Keyframe { Time = beat, Value = value, Ease = Xanadu.Core.Keys.KeyEase.SineInOut });
    }
}

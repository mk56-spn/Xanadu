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
        // A field shader in slot 1, so the host's level-slot path (wrapper, globals,
        // tuned values) is exercised by the demo: slow bands of the level's own hue.
        document.Shaders.Add(new ShaderDocument
        {
            Slot = 1,
            Name = "demo bands",
            Source = """
                import xanadu.colour;

                [Default(0.25)] uniform float speed;
                uniform float4 tint;

                float4 shade(ItemInput i)
                {
                    float band = 0.5 + 0.5 * sin(i.uv.y * 18.0 - i.time * speed * 6.2832);
                    float3 colour = lerp(i.colour.rgb, tint.rgb, band * 0.35);
                    return float4(colour, i.colour.a);
                }
                """,
            Params = { ["tint"] = [0.25f, 0.4f, 0.9f, 1f] },
        });
        document.FieldShader = 1;
        return LevelStore.Entry("", document);
    }
}

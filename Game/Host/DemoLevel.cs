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

        // Step 6's offscreen paths: the long block is on a self-contained layer
        // whose pass ripples it (slot 3); the square one is in a buffered group
        // drawn back through an inverting shader (slot 2), at 80%.
        document.Blocks.Add(new BlockDocument { X = start + 300f, Y = ground - 260f, Width = 480f, Height = 40f, Layer = 5 });
        document.Blocks.Add(new BlockDocument { X = start - 900f, Y = ground - 420f, Width = 300f, Height = 300f, Groups = [0] });
        document.Groups.Add(new GroupDocument { Id = 0, Name = "inverted", Buffered = true, BufferShader = 2, BufferAlpha = 0.8f });
        document.Layers.Add(new LayerDocument { Number = 5, Name = "rippled", Pass = Xanadu.Ecs.LayerPassKind.SelfContained, PassShader = 3 });
        document.Shaders.Add(new ShaderDocument
        {
            Slot = 2,
            Name = "invert",
            Source = "float4 shade(ItemInput i) { return float4(1.0 - i.colour.rgb, i.colour.a); }",
        });
        document.Shaders.Add(new ShaderDocument
        {
            Slot = 3,
            Name = "ripple",
            Source = """
                float4 shade(ItemInput i)
                {
                    float2 uv = i.uv + float2(sin(i.uv.y * 120.0 + i.time * 4.0) * 0.004, 0.0);
                    return sampleItem(uv) * i.vertexColour;
                }
                """,
        });
        // A field shader in slot 1, so the host's level-slot path (wrapper, globals,
        // tuned values) is exercised by the demo: slow bands of the level's own hue.
        document.Shaders.Add(new ShaderDocument
        {
            Slot = 1,
            Name = "demo bands",
            Source = """
                import xanadu;

                [Default(0.25)] uniform float speed;
                uniform float4 tint;

                // Slow bands of the tint, under a rounded frame inset from the field's edge.
                float4 shade(ItemInput i, Canvas canvas)
                {
                    float band = 0.5 + 0.5 * sin(i.uv.y * 18.0 - i.time * speed * TAU);
                    float3 colour = lerp(i.colour.rgb, tint.rgb, band * 0.35);
                    float edge = Box(canvas.size - 80, 32).stroked(canvas.centred, 6.0, 2.0);
                    return float4(lerp(colour, float3(1.0, 0.8, 0.3), edge), i.colour.a);
                }
                """,
            Params = { ["tint"] = [0.25f, 0.4f, 0.9f, 1f] },
        });
        document.FieldShader = 1;
        return LevelStore.Entry("", document);
    }
}

// Rows of music notes drifting across a dark gradient, black unless tinted.
// The notes are font glyphs, rasterised into the `notes` strip (one square
// cell per glyph). Each layer is a grid of square cells; a row of cells slides
// sideways at its own speed and each cell holds at most one note of its own
// size and tilt.
import xanadu;

[Default(2)] uniform int glyph_count;
[Default(1.7778)] uniform float aspect;
// Divides the cell count: a small frame wants a few big notes, not many tiny ones.
[Default(1)] uniform float zoom;
[Default(1)] uniform float speed;
// How loud the music is for each layer, far to near: the far notes follow
// the bass and the near ones the treble. Zero when nothing is playing.
uniform float4 energy;
// 1 while music drives the notes, 0 when nothing is playing. Driven notes
// all but vanish as their layer goes quiet; undriven ones keep their look.
uniform float reactive;
[Default(0.96, 0.77, 0.26, 1)] uniform float4 note_color;
[Default(0, 0, 0, 1)] uniform float4 top_color;
[Default(0, 0, 0, 1)] uniform float4 bottom_color;
// A soft halo that follows each note's outline, in this colour; zero glow
// leaves the notes bare and skips the extra samples.
[Default(1, 0.72, 0.2, 1)] uniform float4 glow_color;
uniform float glow;

[[vk::binding(2, 2)]] Sampler2D notes;

// How far the halo spreads past the ink, as a share of the note's size. The
// strip leaves a tenth of a cell clear on each side, so this stays inside it.
static const float GLOW_REACH = 0.09;

// Paints one layer of notes, if one is at uv: its halo first, added like
// light, then its ink, both by `weight`. `rows` cells span the screen's
// height, so a smaller number means bigger, nearer notes.
void paintNotes(inout colour c, float2 uv, float rows, float drift, float seed, float pulse, float weight)
{
    float2 p = float2(uv.x * aspect, uv.y) * (rows / zoom);

    // Every row of cells moves at its own speed, rightwards.
    float rowSpeed = drift * (0.6 + 0.8 * random(float2(round(p.y), seed)));
    p.x -= time * speed * rowSpeed;

    Tile cell = pattern.tile(p, 1.0);
    float3 h = random3(cell.id + seed);
    if (h.x > 0.4)
        return;

    // Loud passages swell the notes, up to the edge of their cell; each is
    // tilted, scattered inside its cell, and bobs gently.
    float size = min(lerp(0.35, 0.9, h.y) * (1.0 + 0.4 * pulse), 0.98);
    float2 offset = (h.yz - 0.5) * (1.0 - size) * 0.6;
    offset.y += sin(time * speed * 0.8 + h.x * 40.0) * 0.04;
    int glyph = int(mod(floor(h.x * 100.0), float(glyph_count)));
    let note = Sprite(notes, glyph_count, glyph, float2(size))
        .rotated((h.z - 0.5) * 0.9)
        .moved(offset);

    float presence = lerp(1.0, 0.03 + 0.97 * smoothstep(0.0, 0.5, pulse), reactive);
    float strength = presence * (1.0 + 1.0 * pulse);
    float amount = strength * weight;
    if (glow > 0.0)
        c.paint(note.haloed(cell.local, GLOW_REACH * size) * amount * glow, glow_color, blend.add);
    c.paint(note.filled(cell.local) * amount, note_color);
}

float4 shade(ItemInput i)
{
    colour c = Linear(i.canvas.size.y).rotated(PI / 2).coloured(top_color, bottom_color);

    // Far layers first, so the near, large notes sit on top and glow brighter.
    paintNotes(c, i.uv, 14.0, 0.12, 3.0, energy.x, 0.12);
    paintNotes(c, i.uv, 9.0, 0.2, 11.0, energy.y, 0.18);
    paintNotes(c, i.uv, 5.5, 0.32, 23.0, energy.z, 0.28);
    paintNotes(c, i.uv, 3.0, 0.5, 37.0, energy.w, 0.4);

    return float4(c.rgb, 1.0);
}

// Rows of music notes drifting across a dark gradient, black unless tinted.
// The notes are font glyphs, rasterised into the `notes` strip (one square
// cell per glyph). Each layer is a grid of square cells; a row of cells slides
// sideways at its own speed and each cell holds at most one note of its own
// size and tilt. Ported from note_background.gdshader.
import xanadu.stage;

struct Params
{
    [Default(2)] int glyph_count;
    [Default(1.7778)] float aspect;
    // Divides the cell count: a small frame wants a few big notes, not many tiny ones.
    [Default(1)] float zoom;
    [Default(1)] float speed;
    // How loud the music is for each layer, far to near: the far notes follow
    // the bass and the near ones the treble. Zero when nothing is playing.
    float4 energy;
    // 1 while music drives the notes, 0 when nothing is playing. Driven notes
    // all but vanish as their layer goes quiet; undriven ones keep their look.
    float reactive;
    [Default(0.96, 0.77, 0.26, 1)] float4 note_color;
    [Default(0, 0, 0, 1)] float4 top_color;
    [Default(0, 0, 0, 1)] float4 bottom_color;
    // A soft halo that follows each note's outline, in this colour; zero glow
    // leaves the notes bare and skips the extra samples.
    [Default(1, 0.72, 0.2, 1)] float4 glow_color;
    float glow;
};

[[vk::binding(1, 3)]] ConstantBuffer<Params> params;
[[vk::binding(1, 2)]] Sampler2D notes;

// How far the halo spreads past the ink, as a share of the note's box. The
// strip leaves a tenth of a cell clear on each side, so this stays inside it.
static const float GLOW_REACH = 0.09;

// The blurrier mip the halo is read from, so few samples still look smooth.
static const float GLOW_LOD = 3.0;

float hash(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

// The glyph's coverage at q in its box, read at `lod` (below zero picks it
// automatically); nothing outside the box, so neighbouring cells never bleed in.
float glyphInk(float2 q, float glyph, float lod)
{
    if (q.x < 0.0 || q.x > 1.0 || q.y < 0.0 || q.y > 1.0)
        return 0.0;

    float2 uv = float2((q.x + glyph) / float(params.glyph_count), q.y);
    return lod < 0.0 ? notes.Sample(uv).a : notes.SampleLevel(uv, lod).a;
}

// The glyph blurred outwards: a centre and two rings of samples from a blurry
// mip, so the halo hugs the note's outline rather than its box.
float glyphHalo(float2 q, float glyph)
{
    float sum = glyphInk(q, glyph, GLOW_LOD) * 2.0;
    float weight = 2.0;
    for (int i = 0; i < 8; i++)
    {
        float a = float(i) * 0.785398;
        float2 dir = float2(cos(a), sin(a));
        sum += glyphInk(q + dir * GLOW_REACH * 0.5, glyph, GLOW_LOD) * 1.5;
        sum += glyphInk(q + dir * GLOW_REACH, glyph, GLOW_LOD);
        weight += 2.5;
    }

    return sum / weight;
}

// Coverage of the note, if any, at uv in one layer (x), and of its halo (y).
// `rows` cells span the screen's height, so a smaller number means bigger,
// nearer notes.
float2 noteLayer(float2 uv, float time, float rows, float drift, float seed, float pulse)
{
    float2 p = float2(uv.x * params.aspect, uv.y) * (rows / params.zoom);
    float row = floor(p.y);

    // Every row of cells moves at its own speed, rightwards.
    float rowSpeed = drift * (0.6 + 0.8 * hash(float2(row, seed)));
    p.x -= time * params.speed * rowSpeed;

    float2 cell = floor(p);
    float2 local = frac(p) - 0.5;

    float h1 = hash(cell + seed);
    float h2 = hash(cell + seed + 17.0);
    float h3 = hash(cell + seed + 41.0);
    if (h1 > 0.4)
        return float2(0.0);

    // Loud passages swell the notes, up to the edge of their cell.
    float size = min(lerp(0.35, 0.9, h2) * (1.0 + 0.4 * pulse), 0.98);
    float tilt = (h3 - 0.5) * 0.9;

    // Scatter the note inside its cell, then bob it gently.
    float2 offset = (float2(h2, h3) - 0.5) * (1.0 - size) * 0.6;
    offset.y += sin(time * params.speed * 0.8 + h1 * 40.0) * 0.04;
    float2 d = local - offset;

    float c = cos(tilt);
    float s = sin(tilt);
    d = float2(c * d.x + s * d.y, -s * d.x + c * d.y);

    float2 q = d / size + 0.5;
    float margin = params.glow > 0.0 ? GLOW_REACH : 0.0;
    if (q.x < -margin || q.x > 1.0 + margin || q.y < -margin || q.y > 1.0 + margin)
        return float2(0.0);

    float presence = lerp(1.0, 0.03 + 0.97 * smoothstep(0.0, 0.5, pulse), params.reactive);
    float strength = presence * (1.0 + 1.0 * pulse);
    float count = float(params.glyph_count);
    float glyph = floor(h1 * 100.0) - count * floor(floor(h1 * 100.0) / count);
    float ink = glyphInk(q, glyph, -1.0);
    float halo = params.glow > 0.0 ? glyphHalo(q, glyph) : 0.0;
    return float2(ink * strength, halo * strength);
}

// Lays one layer over the colour: its halo first, added like light, then its notes.
float3 layer(float3 color, float2 coverage, float weight)
{
    color += params.glow_color.rgb * coverage.y * weight * params.glow;
    return lerp(color, params.note_color.rgb, coverage.x * weight);
}

[shader("fragment")]
float4 shade(ItemVaryings v) : SV_Target
{
    ItemInput i = input(v);
    float2 uv = i.uv;
    float3 color = lerp(params.top_color.rgb, params.bottom_color.rgb, uv.y);

    // Far layers first, so the near, large notes sit on top and glow brighter.
    float4 energy = params.energy;
    color = layer(color, noteLayer(uv, i.time, 14.0, 0.12, 3.0, energy.x), 0.12);
    color = layer(color, noteLayer(uv, i.time, 9.0, 0.2, 11.0, energy.y), 0.18);
    color = layer(color, noteLayer(uv, i.time, 5.5, 0.32, 23.0, energy.z), 0.28);
    color = layer(color, noteLayer(uv, i.time, 3.0, 0.5, 37.0, energy.w), 0.4);

    return float4(color, 1.0);
}

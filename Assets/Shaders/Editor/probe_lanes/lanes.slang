// The shader editor's motion lanes (HostCore/Tools/ShaderEditing/ProbeLanes.cs): what the open stack's probe nodes
// measured (Slang/ProbeNodes.cs), read straight from the frame's shared probe list. A lane is one copy of a draw over
// the timeline's window: how far it moved (x, y), turned and grew since the window's start. The passes keep the lanes
// that move, and of lanes moving alike only the first, in order; the draw shows each kept lane's moving channels as
// curves on a row of their own, each scaled to its own range. Only the rows go back to the CPU, for the labels, and the clips the clip probe saw, for the clip timeline.
import xanadu;

/// The probe list's shape: lanes, and samples a lane (ProbeNodes.Lanes, ProbeNodes.Samples).
static const uint LANES = 64;
static const uint SAMPLES = 256;

[Shared] [Size(LANES * SAMPLES)] List<float4> xanaduProbes;
[Shared] Stored<uint> xanaduProbeLanes;
/// What the clip probe saw (ClipProbes.Threads × ClipProbes.Region): nothing here reads it, it goes back to the
/// editor's clip timeline as it is.
[Shared] [Reported] [Size(32 * 49)] List<float4> xanaduClips;

/// Each lane's lowest x, y, turn and scale over the window, then its highest: two a lane.
[Size(LANES * 2)] List<float4> range;
/// Each lane's moving channels, a bit each (x 1, y 2, turn 4, scale 8); 0 for a still lane or no lane.
[Size(LANES)] List<uint> moving;
/// 1 for a lane shown: it moves, unlike every lane before it.
[Size(LANES)] List<uint> kept;
/// How many rows there are, then the rows top down: a lane's number, its moving channels times 256. (One list, so the
/// draw reads no more buffers than SDL_GPU binds.)
[Reported] [Size(LANES + 1)] List<uint> rows;

/// A row's height in pixels, and the playhead's place across the window, 0..1.
[Default(18)] uniform float rowHeight;
uniform float now;
/// The colours: the backing and every other row's, the playhead, and each channel's curve (x, y, turn, scale).
uniform float4 backing;
uniform float4 stripe;
uniform float4 playhead;
uniform float4 xLine;
uniform float4 yLine;
uniform float4 turnLine;
uniform float4 scaleLine;

/// Less than this between low and high is standing still: quads for x and y, radians, scale.
static const float4 STILL = float4(1e-3, 1e-3, 2e-3, 2e-3);

float4 probed(uint lane, uint s) { return xanaduProbes[lane * SAMPLES + s]; }

[Pass(64, over: LANES)]
void measure(uint i)
{
    float4 lo = float4(1e30), hi = float4(-1e30);
    bool real = i < xanaduProbeLanes && probed(i, 0).w > 0;
    for (uint s = 0; s < SAMPLES; s++)
    {
        float4 v = probed(i, s);
        lo = min(lo, v);
        hi = max(hi, v);
    }

    range[i * 2] = lo;
    range[i * 2 + 1] = hi;
    uint mask = 0;
    for (uint c = 0; c < 4; c++)
        if (real && hi[c] - lo[c] > STILL[c])
            mask |= 1u << c;
    moving[i] = mask;
}

[Pass(64, over: LANES)]
void keep(uint i)
{
    uint mask = moving[i];
    bool alone = mask != 0;
    for (uint j = 0; j < i && alone; j++)
    {
        if (moving[j] != mask)
            continue;
        bool same = true;
        for (uint s = 0; s < SAMPLES && same; s += 4)
            same = all(abs(probed(i, s) - probed(j, s)) <= STILL * 2);
        alone = !same;
    }

    kept[i] = alone ? 1 : 0;
}

[Pass(64, over: LANES)]
void rank(uint i)
{
    uint before = 0;
    for (uint j = 0; j < i; j++)
        before += kept[j];
    if (kept[i] != 0)
        rows[1 + before] = i | (moving[i] << 8);
    if (i == LANES - 1)
        rows[0] = before + kept[i];
}

/// How near the pixel is to channel c's curve on its row, as coverage: 1 on the line, 0 a pixel and a half off it.
float curve(uint lane, uint c, float2 px, float top, Canvas canvas)
{
    float lo = range[lane * 2][c], span = max(range[lane * 2 + 1][c] - lo, 1e-9);
    float at = saturate(px.x / canvas.size.x) * (SAMPLES - 1);
    uint s = min(uint(at), SAMPLES - 2);
    float a = (probed(lane, s)[c] - lo) / span, b = (probed(lane, s + 1)[c] - lo) / span;
    float tall = rowHeight - 6;
    float y = top + 3 + (1 - lerp(a, b, at - s)) * tall;
    float slope = (a - b) * tall * (SAMPLES - 1) / canvas.size.x;
    return saturate(1.5 - abs(px.y - y) / sqrt(1 + slope * slope));
}

[Draw(1, 1, shade: look)]
ItemVertex lanes(ItemVertex v) { return v; }

float4 look(ItemInput i, Canvas canvas)
{
    float2 px = i.uv * canvas.size;
    uint r = uint(px.y / rowHeight);
    float4 c = r % 2 == 0 ? backing : stripe;
    if (r < rows[0])
    {
        uint row = rows[1 + r], lane = row & 0xFF, mask = row >> 8;
        float4 lines[4] = { xLine, yLine, turnLine, scaleLine };
        for (uint k = 0; k < 4; k++)
            if ((mask >> k & 1) != 0)
                c = lerp(c, lines[k], curve(lane, k, px, r * rowHeight, canvas) * lines[k].a);
    }

    if (abs(px.x - now * canvas.size.x) < 1)
        c = playhead;
    return float4(c.rgb, 1);
}

void graph()
{
    measure();
    keep();
    rank();
    lanes(frame);
}

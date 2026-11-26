// The shader editor's motion lanes (HostCore/Tools/ShaderEditing/ProbeLanes.cs): what the open stack's probe nodes
// measured (Slang/ProbeNodes.cs), read straight from the frame's shared probe lists. A motion lane is one copy of a draw
// over the timeline's window: how far it moved (x, y), turned and grew since the window's start. A colour lane is one
// shade (or one copy of a draw's shade): what it painted at four places on the quad (ColourProbes). The passes keep the
// lanes that change, and of lanes changing alike only the first, in order: motion lanes, then colour lanes. The draw
// shows a kept motion lane's moving channels as curves, each scaled to its own range, and a colour lane as four strips,
// one a place. Only the rows go back to the CPU, for the labels, and what the clip probe saw, for the clip timeline.
import xanadu;

/// The probe lists' shape: lanes, samples a lane, places a colour lane looks at (ProbeNodes, ColourProbes).
static const uint LANES = 64;
static const uint SAMPLES = 256;
static const uint POINTS = 4;

[Shared] [Size(LANES * SAMPLES)] List<float4> xanaduProbes;
[Shared] Stored<uint> xanaduProbeLanes;
[Shared] [Size(LANES * SAMPLES * POINTS)] List<float4> xanaduProbeColours;
[Shared] Stored<uint> xanaduColourLanes;
/// What the clip probe saw (ClipProbes.Threads × ClipProbes.Region): nothing here reads it, it goes back to the
/// editor's clip timeline as it is.
[Shared] [Reported] [Size(32 * 49)] List<float4> xanaduClips;

/// Each motion lane's lowest x, y, turn and scale over the window, then its highest: two a lane.
[Size(LANES * 2)] List<float4> range;
/// Each lane's changing channels, a bit each (x 1, y 2, turn 4, scale 8, colour 16); 0 for a still lane or no lane.
/// Motion lanes first, then colour lanes.
[Size(LANES * 2)] List<uint> moving;
/// 1 for a lane shown: it changes, unlike every lane of its kind before it.
[Size(LANES * 2)] List<uint> kept;
/// How many rows there are, then the rows top down: a lane's number, its changing channels times 256. (One list, so
/// the draw reads no more buffers than SDL_GPU binds.)
[Reported] [Size(LANES * 2 + 1)] List<uint> rows;

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

/// Less than this between low and high is standing still: quads for x and y, radians, scale; and a colour's channel.
static const float4 STILL = float4(1e-3, 1e-3, 2e-3, 2e-3);
static const float STILL_COLOUR = 2.0 / 255.0;
static const uint COLOUR = 16;

float4 probed(uint lane, uint s) { return xanaduProbes[lane * SAMPLES + s]; }

float4 painted(uint lane, uint s, uint p) { return xanaduProbeColours[(lane * SAMPLES + s) * POINTS + p]; }

[Pass(64, over: LANES * 2)]
void measure(uint i)
{
    if (i >= LANES)
    {
        // A place changing over time, not places differing from each other (a checker is still).
        uint lane = i - LANES;
        bool changes = false;
        for (uint p = 0; p < POINTS && lane < xanaduColourLanes && !changes; p++)
        {
            float4 lo = float4(1e30), hi = float4(-1e30);
            for (uint s = 0; s < SAMPLES; s++)
            {
                float4 v = painted(lane, s, p);
                lo = min(lo, v);
                hi = max(hi, v);
            }
            changes = any(hi - lo > STILL_COLOUR);
        }

        moving[i] = changes ? COLOUR : 0;
        return;
    }

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

/// Whether two lanes of one kind (both motion or both colour, by their place in `moving`) change alike.
bool alike(uint a, uint b)
{
    bool same = true;
    if (a < LANES)
    {
        for (uint s = 0; s < SAMPLES && same; s += 4)
            same = all(abs(probed(a, s) - probed(b, s)) <= STILL * 2);
        return same;
    }

    for (uint s = 0; s < SAMPLES && same; s += 4)
        for (uint p = 0; p < POINTS && same; p++)
            same = all(abs(painted(a - LANES, s, p) - painted(b - LANES, s, p)) <= STILL_COLOUR * 2);
    return same;
}

[Pass(64, over: LANES * 2)]
void keep(uint i)
{
    uint mask = moving[i];
    bool alone = mask != 0;
    for (uint j = i < LANES ? 0 : LANES; j < i && alone; j++)
        alone = moving[j] != mask || !alike(i, j);
    kept[i] = alone ? 1 : 0;
}

[Pass(64, over: LANES * 2)]
void rank(uint i)
{
    uint before = 0;
    for (uint j = 0; j < i; j++)
        before += kept[j];
    if (kept[i] != 0)
        rows[1 + before] = (i % LANES) | (moving[i] << 8);
    if (i == LANES * 2 - 1)
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
        if ((mask & COLOUR) != 0)
        {
            // Four strips, one a place the shade was looked at, its colour over time.
            float within = (px.y - r * rowHeight - 2) / (rowHeight - 4);
            if (within >= 0 && within < 1)
            {
                uint s = min(uint(saturate(px.x / canvas.size.x) * SAMPLES), SAMPLES - 1);
                float4 shown = painted(lane, s, min(uint(within * POINTS), POINTS - 1));
                c = lerp(c, float4(shown.rgb, 1), saturate(shown.a));
            }
        }
        else
        {
            float4 lines[4] = { xLine, yLine, turnLine, scaleLine };
            for (uint k = 0; k < 4; k++)
                if ((mask >> k & 1) != 0)
                    c = lerp(c, lines[k], curve(lane, k, px, r * rowHeight, canvas) * lines[k].a);
        }
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

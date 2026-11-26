// The shader editor's motion lanes (HostCore/Tools/ShaderEditing/ProbeLanes.cs): what the open stack's probe nodes
// measured (Slang/ProbeNodes.cs), read straight from the frame's shared probe lists. A motion lane is one copy of a draw
// over the timeline's window: how far it moved (x, y), turned (unwrapped, so a spin is a ramp) and grew since the
// window's start. A colour lane is one shade (or one copy of a draw's shade): what it painted at four places on the quad
// (ColourProbes). A recorded lane is one number the stack keeps from frame to frame (RecordProbes), over the last FRAMES
// frames the clock moved, newest on the right: it replays, it doesn't scrub, so no playhead crosses it.
//
// A row is a draw, not a copy: a draw's copies run the same code, so its changing lanes fold into its first one
// (`motionStarts` / `colourStarts` mark where each draw's lanes start). A motion row gives each channel any copy moves a
// band of its own, every copy's curve drawn in it over the group's range (the first bright, the rest faint, so a stagger
// shows as a spread); a colour row shows its first copy's four strips. Recorded lanes fold only when they change alike.
// Only the rows go back to the CPU, for the labels and heights, and what the clip probe saw, for the clip timeline.
import xanadu;

/// The probe lists' shape: lanes, samples a lane, places a colour lane looks at (ProbeNodes, ColourProbes).
static const uint LANES = 64;
static const uint SAMPLES = 256;
static const uint POINTS = 4;
static const uint FRAMES = 256;
/// Where `range` holds: each recorded lane's low and high; the frames held; the recorded lanes' frames; each motion
/// lane's samples, turn unwrapped; each motion row's low and high over its copies.
static const uint HELD = LANES * 3;
static const uint RECORDS = HELD + 1;
static const uint MOTION = RECORDS + LANES * FRAMES;
static const uint GROUP = MOTION + LANES * SAMPLES;

[Shared] [Size(LANES * SAMPLES)] List<float4> xanaduProbes;
[Shared] Stored<uint> xanaduProbeLanes;
[Shared] [Size(LANES * SAMPLES * POINTS)] List<float4> xanaduProbeColours;
[Shared] Stored<uint> xanaduColourLanes;
/// The recorded ring (RecordProbes): a lane of FRAMES a number; head is (frames recorded, last time, code, lanes).
[Shared] [Size(LANES * FRAMES)] List<float> xanaduRecords;
[Shared] Stored<uint4> xanaduRecordHead;
/// What the clip probe saw (ClipProbes.Threads × ClipProbes.Region): nothing here reads it, it goes back to the
/// editor's clip timeline as it is.
[Shared] [Reported] [Size(32 * 49)] List<float4> xanaduClips;

/// Each motion lane's lowest x, y, turn and scale over the window, then its highest: two a lane; then the regions named
/// above. (One list, so the draw never reads the probes or the ring itself: SDL_GPU binds it no more buffers.)
[Size(GROUP + LANES * 2)] List<float4> range;
/// Each lane's changing channels, a bit each (x 1, y 2, turn 4, scale 8, colour 16, recorded 32); 0 for a still lane or
/// no lane. Motion lanes first, then colour lanes, then recorded ones.
[Size(LANES * 3)] List<uint> moving;
/// 1 for a lane that leads a row; 2 + its leader for one folded into another's row; 0 for neither.
[Size(LANES * 3)] List<uint> kept;
/// How many rows there are, then the rows top down: the leading lane, the channels any of its lanes change << 8, the
/// lanes after the leader its row spans << 16, how many lanes fold into it << 24.
[Reported] [Size(LANES * 3 + 1)] List<uint> rows;

/// A band's height in pixels (a motion row is one a moving channel), a colour row's, and the playhead's place, 0..1.
[Default(14)] uniform float bandHeight;
[Default(24)] uniform float rowHeight;
uniform float now;
/// Where each draw's lanes start, a bit a lane, 16 a component (motion, then colour): set by the editor from the labels.
uniform float4 motionStarts;
uniform float4 colourStarts;
/// The colours: the backing and every other row's, the playhead, and each channel's curve (x, y, turn, scale).
uniform float4 backing;
uniform float4 stripe;
uniform float4 playhead;
uniform float4 xLine;
uniform float4 yLine;
uniform float4 turnLine;
uniform float4 scaleLine;
uniform float4 recordLine;

/// Less than this between low and high is standing still: quads for x and y, radians, scale; and a colour's channel.
static const float4 STILL = float4(1e-3, 1e-3, 2e-3, 2e-3);
static const float STILL_COLOUR = 2.0 / 255.0;
static const uint COLOUR = 16;
static const uint RECORDED = 32;
/// How strongly a row's other copies show beside its first.
static const float OTHERS = 0.35;

float4 probed(uint lane, uint s) { return xanaduProbes[lane * SAMPLES + s]; }

/// A motion lane's sample as measure copied it, turn unwrapped: what every pass after measure and the draw read.
float4 motion(uint lane, uint s) { return range[MOTION + lane * SAMPLES + s]; }

float4 painted(uint lane, uint s, uint p) { return xanaduProbeColours[(lane * SAMPLES + s) * POINTS + p]; }

/// Frames in the ring, up to FRAMES.
uint heldFrames() { return min(xanaduRecordHead.x, FRAMES); }

/// A recorded lane's s-th frame of the held ones, oldest first, from the ring.
float recorded(uint lane, uint s) { return xanaduRecords[lane * FRAMES + (xanaduRecordHead.x - heldFrames() + s) % FRAMES]; }

/// The same from range's copy, for the passes after measureRecords and the draw.
float copied(uint lane, uint s) { return range[RECORDS + lane * FRAMES + s].x; }

/// Whether a recorded number moved between lo and hi: by more than its size's share, so counts in the thousands don't
/// show float noise and a 0..1 flag still shows.
bool budged(float lo, float hi) { return hi - lo > 1e-5 * max(1, max(abs(lo), abs(hi))); }

/// Whether lane (of its kind) starts a draw's lanes: the first lane always does.
bool starts(uint lane, float4 bits) { return lane == 0 || (uint(bits[lane / 16]) >> (lane % 16) & 1) != 0; }

/// The first lane of the draw `lane` belongs to.
uint drawOf(uint lane, float4 bits)
{
    uint first = lane;
    while (!starts(first, bits))
        first--;
    return first;
}

[Pass(64, over: LANES)]
void measureRecords(uint lane)
{
    uint held = heldFrames();
    float lo = 1e30, hi = -1e30;
    for (uint s = 0; s < held; s++)
    {
        float v = recorded(lane, s);
        range[RECORDS + lane * FRAMES + s] = float4(v, 0, 0, 0);
        lo = min(lo, v);
        hi = max(hi, v);
    }

    range[LANES * 2 + lane] = float4(lo, hi, 0, 0);
    if (lane == 0)
        range[HELD] = float4(held, 0, 0, 0);
    moving[LANES * 2 + lane] = lane < (xanaduRecordHead.w & 0xFFFF) && held > 1 && budged(lo, hi) ? RECORDED : 0;
}

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
    float turned = 0;
    for (uint s = 0; s < SAMPLES; s++)
    {
        float4 v = probed(i, s);
        // The turn goes on from the last sample's rather than jumping at a half turn.
        if (s > 0)
            v.z = turned + (v.z - turned) - TAU * round((v.z - turned) / TAU);
        turned = v.z;
        range[MOTION + i * SAMPLES + s] = v;
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

/// Whether two recorded lanes change alike.
bool alike(uint a, uint b)
{
    bool same = true;
    for (uint s = 0; s < uint(range[HELD].x) && same; s++)
    {
        float u = copied(a, s), v = copied(b, s);
        same = !budged(min(u, v), max(u, v));
    }
    return same;
}

[Pass(64, over: LANES * 3)]
void keep(uint i)
{
    uint kind = i / LANES, lane = i % LANES;
    kept[i] = moving[i] != 0 ? 1 : 0;
    if (moving[i] == 0)
        return;
    // Motion and colour: the draw's first changing lane leads. Recorded: the first lane changing alike.
    uint from = kind == 2 ? 0 : drawOf(lane, kind == 0 ? motionStarts : colourStarts);
    for (uint j = from; j < lane; j++)
        if (moving[kind * LANES + j] != 0 && (kind != 2 || moving[kind * LANES + j] == moving[i] && alike(lane, j)))
        {
            kept[i] = 2 + kind * LANES + j;
            return;
        }
}

[Pass(64, over: LANES * 3)]
void rank(uint i)
{
    uint before = 0;
    for (uint j = 0; j < i; j++)
        before += kept[j] == 1 ? 1 : 0;
    if (i == LANES * 3 - 1)
        rows[0] = before + (kept[i] == 1 ? 1 : 0);
    if (kept[i] != 1)
        return;

    // What folds into it: the channels any of them change, how far they reach, how many; and, for motion, their range.
    uint mask = moving[i], last = i, folded = 0;
    float4 lo = range[(i % LANES) * 2], hi = range[(i % LANES) * 2 + 1];
    for (uint j = i + 1; j < (i / LANES + 1) * LANES; j++)
    {
        if (kept[j] != 2 + i)
            continue;
        mask |= moving[j];
        last = j;
        folded++;
        if (i < LANES)
        {
            lo = min(lo, range[j * 2]);
            hi = max(hi, range[j * 2 + 1]);
        }
    }

    if (i < LANES)
    {
        range[GROUP + i * 2] = lo;
        range[GROUP + i * 2 + 1] = hi;
    }
    rows[1 + before] = (i % LANES) | (mask << 8) | (min(last - i, 255) << 16) | (min(folded, 255) << 24);
}

/// A row's height: a band a moving channel for motion, one band for a recorded number, rowHeight for colour. The
/// editor works out the same (ProbeLanes.Tall).
float tall(uint row)
{
    uint mask = row >> 8 & 0xFF;
    if ((mask & COLOUR) != 0)
        return rowHeight;
    return 4 + max(countbits(mask & 15), 1) * bandHeight;
}

/// How near the pixel is to a line through a and b (0..1 of the band), `within` of the way from a: as coverage, 1 on
/// the line, 0 a pixel and a half off it.
float near(float a, float b, float within, float2 px, float top, float band, float perPixel)
{
    float y = top + (1 - lerp(a, b, within)) * band;
    float slope = (a - b) * band * perPixel;
    return saturate(1.5 - abs(px.y - y) / sqrt(1 + slope * slope));
}

/// Channel c of a motion lane at the pixel, over its row's range.
float curve(uint lane, uint leader, uint c, float2 px, float top, float band, Canvas canvas)
{
    float lo = range[GROUP + leader * 2][c], span = max(range[GROUP + leader * 2 + 1][c] - lo, 1e-9);
    float at = saturate(px.x / canvas.size.x) * (SAMPLES - 1);
    uint s = min(uint(at), SAMPLES - 2);
    return near((motion(lane, s)[c] - lo) / span, (motion(lane, s + 1)[c] - lo) / span, at - s, px, top, band,
        (SAMPLES - 1) / canvas.size.x);
}

/// A recorded lane at the pixel: the held frames right-aligned, the newest at the right edge.
float recordedCurve(uint lane, float2 px, float top, float band, Canvas canvas)
{
    uint held = uint(range[HELD].x);
    float at = saturate(px.x / canvas.size.x) * (FRAMES - 1) - (FRAMES - held);
    if (held < 2 || at < 0)
        return 0;
    float lo = range[LANES * 2 + lane].x, span = max(range[LANES * 2 + lane].y - lo, 1e-9);
    uint s = min(uint(at), held - 2);
    return near((copied(lane, s) - lo) / span, (copied(lane, s + 1) - lo) / span, saturate(at - s), px, top, band,
        (FRAMES - 1) / canvas.size.x);
}

[Draw(1, 1, shade: look)]
ItemVertex lanes(ItemVertex v) { return v; }

float4 look(ItemInput i, Canvas canvas)
{
    float2 px = i.uv * canvas.size;
    // The row under the pixel, rows being as tall as they need.
    uint r = 0;
    float top = 0;
    while (r < rows[0] && top + tall(rows[1 + r]) <= px.y)
        top += tall(rows[1 + r++]);
    float4 c = r % 2 == 0 ? backing : stripe;
    if (r >= rows[0])
        return float4(c.rgb, 1);

    uint row = rows[1 + r], lane = row & 0xFF, mask = row >> 8 & 0xFF, reach = row >> 16 & 0xFF;
    if ((mask & RECORDED) != 0)
    {
        c = lerp(c, recordLine, recordedCurve(lane, px, top + 3.5, bandHeight - 3, canvas) * recordLine.a);
        return float4(c.rgb, 1);
    }

    if ((mask & COLOUR) != 0)
    {
        // Four strips, one a place the shade was looked at, its colour over time.
        float within = (px.y - top - 2) / (rowHeight - 4);
        if (within >= 0 && within < 1)
        {
            uint s = min(uint(saturate(px.x / canvas.size.x) * SAMPLES), SAMPLES - 1);
            float4 shown = painted(lane, s, min(uint(within * POINTS), POINTS - 1));
            c = lerp(c, float4(shown.rgb, 1), saturate(shown.a));
        }
    }
    else
    {
        // A band each moving channel, top down in channel order; in it every copy of the row, the first on top.
        float4 lines[4] = { xLine, yLine, turnLine, scaleLine };
        uint band = 0;
        for (uint k = 0; k < 4; k++)
        {
            if ((mask >> k & 1) == 0)
                continue;
            float from = top + 2 + band++ * bandHeight;
            if (px.y < from || px.y >= from + bandHeight)
                continue;
            float others = 0;
            for (uint m = lane + 1; m <= lane + reach; m++)
                if ((moving[m] >> k & 1) != 0)
                    others = max(others, curve(m, lane, k, px, from + 1.5, bandHeight - 3, canvas));
            c = lerp(c, lines[k], others * OTHERS * lines[k].a);
            c = lerp(c, lines[k], curve(lane, lane, k, px, from + 1.5, bandHeight - 3, canvas) * lines[k].a);
        }
    }

    if (abs(px.x - now * canvas.size.x) < 1)
        c = playhead;
    return float4(c.rgb, 1);
}

void graph()
{
    measure();
    measureRecords();
    keep();
    rank();
    lanes(frame);
}

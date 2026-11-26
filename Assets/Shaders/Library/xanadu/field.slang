// xanadu.field: a grid of floats over a 0..1 square that anything can write into and read from,
// fading and spreading into its neighbours each frame. A block: declared once, it brings its
// lists and its pass with it.
//
//     [Size(640, 360)] Field field;          // 640 x 360 cells
//     field.deposit(uv, amount);             // from any pass or draw, many threads at once
//     float here = field.at(uv);             // last frame's, blended between cells
//     float2 rise = field.slope(uv, probe);  // which way it climbs, probe in uv either side
//     float shown = field.shown(uv);         // this frame's, for a draw after the spread
//     void graph() { …; field.spread(fade, spread); }   // fade a second, share spread a frame
//
// A frame reads last frame's field and deposits into this one; spread then makes this frame's
// from last frame's, spread and faded, with the deposits added. Deposits sum as whole numbers,
// DEPOSIT_SCALE a unit.
module field;

import xanadu.store;
import xanadu.stage;

public static const float DEPOSIT_SCALE = 1024;

/// A field of w × h cells: [Size(w, h)] Field name;.
[Block(w, h)]
public struct Field
{
    public uint w, h;
    [Size(w * h)] List<float> cells;          // this frame's, written whole by spread
    [History(cells)] List<float> before;      // last frame's, which at() reads
    [Size(w * h)] List<uint> deposits;        // this frame's, whole numbers

    /// The cell a 0..1 position falls in.
    public uint cellOf(float2 uv)
    {
        uint2 c = uint2(clamp(uv * float2(w, h), 0, float2(w, h) - 1));
        return c.y * w + c.x;
    }

    /// A list's value at a 0..1 position, blended between its four nearest cells.
    float blended(List<float> from, float2 uv)
    {
        float2 g = uv * float2(w, h) - 0.5;
        float2 f = frac(g);
        int2 most = int2(w, h) - 1;
        int2 c0 = clamp(int2(floor(g)), 0, most), c1 = clamp(int2(floor(g)) + 1, 0, most);
        float a = from[c0.y * w + c0.x], b = from[c0.y * w + c1.x];
        float d = from[c1.y * w + c0.x], e = from[c1.y * w + c1.x];
        return lerp(lerp(a, b, f.x), lerp(d, e, f.x), f.y);
    }

    /// Last frame's field at a 0..1 position.
    public float at(float2 uv) { return blended(before, uv); }

    /// This frame's field at a 0..1 position: read it after the spread (in a draw).
    public float shown(float2 uv) { return blended(cells, uv); }

    /// Which way last frame's field climbs at uv, measured probe either side.
    public float2 slope(float2 uv, float2 probe)
    {
        return float2(at(uv + float2(probe.x, 0)) - at(uv - float2(probe.x, 0)),
                      at(uv + float2(0, probe.y)) - at(uv - float2(0, probe.y)));
    }

    /// Adds amount at uv's cell this frame, safely from many threads.
    public void deposit(float2 uv, float amount)
    {
        InterlockedAdd(deposits[cellOf(uv)], uint(max(amount, 0) * DEPOSIT_SCALE));
    }

    /// Cell i of this frame made from last frame's: spread into its neighbours by `amount`,
    /// faded `fade` a second, with the deposits added and taken.
    public void spreadCell(uint i, float fade, float amount)
    {
        int2 c = int2(i % w, i / w);
        int2 most = int2(w, h) - 1;
        float sum = 0;
        for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
            {
                int2 n = clamp(c + int2(x, y), 0, most);
                sum += before[n.y * w + n.x];
            }
        float spreadOut = lerp(before[i], sum / 9, saturate(amount));
        cells[i] = spreadOut * exp(-fade * dt) + float(deposits[i]) / DEPOSIT_SCALE;
        deposits[i] = 0;
    }
};

public namespace field
{
    /// This frame's field from last frame's: every cell spread, faded and deposited into.
    [Pass(64, over: w * h)]
    public void spread(uint i, Field f, float fade, float amount) { f.spreadCell(i, fade, amount); }
}

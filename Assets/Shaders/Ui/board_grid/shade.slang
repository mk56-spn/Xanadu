// A board's backing: its colour, with a small square dot at every grid point, worked out per pixel from where the
// board is panned and zoomed to, so a still or moving grid costs no drawing on the CPU at any zoom.
import xanadu;

/// The backing's colour, and the dots' (their alpha is how strongly they show over it).
uniform float4 backing;
uniform float4 dot;
/// The board's box, in the pixels the rect is drawn in.
[Default(1, 1)] uniform float2 area;
/// The board point at the box's top-left corner, and pixels per board unit.
uniform float2 pan;
[Default(1)] uniform float zoom;
/// Board units between two grid points.
[Default(50)] uniform float step;

/// Half a dot's size, in pixels.
static const float DOT_HALF = 1;

float4 shade(ItemInput i)
{
    float2 board = pan + i.uv * area / zoom;
    // Pixels from here to the nearest grid point.
    float2 off = (frac(board / step + 0.5) - 0.5) * step * zoom;
    float on = all(abs(off) <= DOT_HALF) ? dot.a : 0;
    return float4(lerp(backing.rgb, dot.rgb, on), backing.a);
}

// A dash ender: a see-through block with a strong border and a bar across its
// middle each way (a stop sign for dashes: a dash ends on the centre, whichever
// way it came), over a soft glow. Drawn on a quad a little bigger than the
// block so the glow has room. Sizes are item units, made screen pixels with
// xif_unit.
import xanadu;

// Screen pixels per item unit; the drawer sets it.
[Default(1)] uniform float xif_unit;
// The block's half width and half height (item units).
[Default(55, 55)] uniform float2 stop_half;
// The border's width, the bars' thickness, and how far the glow reaches past the block (item units).
[Default(3, 4, 30)] uniform float3 stop_lines;
[Default(1, 0.55, 0.25, 1)] uniform colour stop_tint;

float4 shade(ItemInput i)
{
    float unit = max(xif_unit, 1e-4);
    float2 size = stop_half * 2 * unit;
    float bar = stop_lines.y * unit;

    let block = Box(size).measured();
    let bars = Box(float2(size.x * 0.55, bar)).or(Box(float2(bar, size.y * 0.55)));

    colour c = colours.transparent;
    c.paint(block.feathered(stop_lines.z * unit), stop_tint.withAlpha(0.18));
    c.paint(block, Look(stop_tint.withAlpha(0.22), stop_tint.withAlpha(0.95), stop_lines.x * unit));
    c.paint(bars, stop_tint.withAlpha(0.9));

    // The item's colour carries its self modulate and fades.
    return c * i.colour;
}

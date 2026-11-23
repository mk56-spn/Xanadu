// A dash ender: a see-through block with a strong border and a bar across its
// middle each way (a stop sign for dashes: a dash ends on the centre, whichever
// way it came), over a soft glow. Drawn on a quad a little bigger than the
// block so the glow has room. Sizes are in item units (playfield units), made
// screen pixels with xif_unit.
import xanadu;

[Default(1)] uniform float xif_unit;
// The block's half width and half height, its border's width and its bars' (item units).
[Default(55, 55, 3, 4)] uniform float4 stop_block;
// The stop's colour.
[Default(1, 0.55, 0.25, 1)] uniform float4 stop_tint;

float4 shade(ItemInput i)
{
    float unit = max(xif_unit, 1e-4);
    float soft = 1.0 / unit;
    float2 p = i.canvas.centred / unit;
    float2 half = stop_block.xy;
    float3 tint = stop_tint.rgb;

    // The glow: an ellipse half again the block's size, fading out to its rim.
    float reach = saturate(1.0 - length(p / (half * 1.5)));
    colour c = colours.transparent;
    c.paint(reach, float4(tint, 0.18));

    float d = Box(half * 2.0).local(p);
    c.paint(sdf.filled(d, soft), float4(tint, 0.22));
    c.paint(sdf.stroked(d + stop_block.z * 0.5, stop_block.z, soft), float4(tint, 0.95));

    float bar = stop_block.w;
    float across = Box(float2(half.x * 1.1, bar)).local(p);
    float down = Box(float2(bar, half.y * 1.1)).local(p);
    c.paint(sdf.filled(min(across, down), soft), float4(tint, 0.9));

    // The vertex colour carries self modulate and fades.
    return float4(c.rgb * i.colour.rgb, c.a * i.colour.a);
}

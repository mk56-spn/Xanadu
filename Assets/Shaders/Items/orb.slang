// An orb: a bright core with a white centre in a soft glow, brighter while it is
// ridden, and while it waits for its head a ring closing onto it. The drawer
// sets orb_tint (the orb's colour: grey once missed) and orb_state from the orb
// each frame. Drawn on a quad with room for the glow and the ring. Sizes are in
// item units (playfield units), made screen pixels with xif_unit.
import xanadu;

[Default(1)] uniform float xif_unit;
// The core's radius (item units).
[Default(22)] uniform float orb_radius;
// The orb's colour.
[Default(1, 0.82, 0.35, 1)] uniform float4 orb_tint;
// How shown it is (0..1), lit while ridden (0 or 1), its approach (1 far, 0 at its head; below 0 none).
[Default(1, 0, -1, 0)] uniform float4 orb_state;

float4 shade(ItemInput i)
{
    float unit = max(xif_unit, 1e-4);
    float px = 1.0 / unit;
    float r = length(i.canvas.centred / unit);
    float radius = orb_radius;
    float alpha = orb_state.x;
    float lit = orb_state.y;
    float3 tint = orb_tint.rgb;
    colour c = colours.transparent;

    float glow = saturate(1.0 - r / (radius * 2.6));
    c.paint(glow, float4(tint, lerp(0.3, 0.55, lit) * alpha));
    c.paint(sdf.filled(r - radius, px), float4(tint, alpha));
    c.paint(sdf.filled(r - radius * 0.5, px), float4(1.0, 1.0, 1.0, lerp(0.6, 0.95, lit) * alpha));

    // The ring closing onto it before its head, 3 pixels wide.
    float left = orb_state.z;
    if (left >= 0.0 && left <= 1.0)
        c.paint(sdf.stroked(r - radius * (1.2 + 3.0 * left), 3.0 * px, px), float4(tint, alpha * (1.0 - left)));

    // The vertex colour carries self modulate and fades.
    return float4(c.rgb * i.colour.rgb, c.a * i.colour.a);
}

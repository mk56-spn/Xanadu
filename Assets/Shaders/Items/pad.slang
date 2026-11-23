// A pad: a low glowing slab with three chevrons pointing up. When it fires
// (pad_state.x, the spring: 1 as it fires, down to 0) it flashes, the chevrons
// spring up off it and a column of light shoots up. A timed pad shows a ring
// closing onto it over its approach (pad_state.y: 1 far, 0 at its time; below 0
// none), and in the editor a ring marking it as timed (pad_state.z: the ring's
// alpha; 0 none). The drawer sets pad_state from the pad each frame. Drawn on a
// quad with room above for the column and round it for the rings. Sizes are in
// item units (playfield units), made screen pixels with xif_unit.
import xanadu;

[Default(1)] uniform float xif_unit;
// The slab's half width and half height (item units).
[Default(75, 14)] uniform float2 pad_half;
// The pad's colour.
[Default(1, 0.35, 0.75, 1)] uniform float4 pad_tint;
// The spring, the approach and the editor's ring, as above.
[Default(0, -1, 0, 0)] uniform float4 pad_state;

// A chevron pointing up, its tip at `tip`, `w` wide each side and `h` deep: a distance.
float chevron(float2 p, float2 tip, float w, float h)
{
    return min(Line(tip + float2(-w, h), tip).local(p), Line(tip, tip + float2(w, h)).local(p));
}

float4 shade(ItemInput i)
{
    float unit = max(xif_unit, 1e-4);
    float px = 1.0 / unit;
    float2 p = i.canvas.centred / unit;
    float2 h = pad_half;
    float3 tint = pad_tint.rgb;
    float spring = saturate(pad_state.x);
    colour c = colours.transparent;

    // Glow under it, brighter as it fires.
    float glow = saturate(1.0 - length(p / float2(h.x * 1.4, h.y * 3.0)));
    c.paint(glow, float4(tint, 0.25 + 0.4 * spring));

    // The column of light it launches up, fading towards its top.
    if (spring > 0.0)
    {
        float height = h.x * 4.0 * (1.0 - spring * spring);
        float up = (-h.y - p.y) / max(height, 1e-4);
        float inside = step(abs(p.x), h.x * 0.5) * step(0.0, up) * step(up, 1.0);
        c.paint(inside, float4(tint, 0.6 * spring * (1.0 - up)));
    }

    // The slab, its bright border, and a white flash over it as it fires.
    float d = Box(h * 2.0).local(p);
    c.paint(sdf.filled(d, px), float4(tint, 0.9));
    float border = max(1.0 * px, 2.0);
    c.paint(sdf.stroked(d + border * 0.5, border, px), float4(1.0, 1.0, 1.0, 0.7 + 0.3 * spring));
    c.paint(sdf.filled(d, px), float4(1.0, 1.0, 1.0, 0.7 * spring));

    // Three chevrons over it, springing up as it fires.
    float lift = h.y * (1.2 + 3.0 * spring);
    float line = max(2.0 * px, 3.0);
    float marks = 1e9;
    for (int k = 0; k < 3; k++)
    {
        float2 tip = float2(float(k - 1) * h.x * 0.55, -h.y - lift * (0.6 + 0.25 * k));
        marks = min(marks, chevron(p, tip, h.x * 0.22, h.y * 0.9));
    }
    c.paint(sdf.filled(marks - line * 0.5, px), float4(1.0, 1.0, 1.0, 0.55 + 0.45 * spring));

    // The editor's ring marking a timed pad.
    float r = length(p);
    if (pad_state.z > 0.0)
        c.paint(sdf.stroked(r - h.x * 1.3, 2.0 * px, px), float4(tint, pad_state.z));

    // The approach: a ring closing onto it, thickening and brightening as its time comes.
    float t = pad_state.y;
    if (t > 0.0 && t <= 1.0)
        c.paint(sdf.stroked(r - h.x * (1.1 + 2.2 * t), (2.0 + 3.0 * (1.0 - t)) * px, px), float4(1.0, 1.0, 1.0, 0.9 * (1.0 - t)));

    // The vertex colour carries self modulate and fades.
    return float4(c.rgb * i.colour.rgb, c.a * i.colour.a);
}

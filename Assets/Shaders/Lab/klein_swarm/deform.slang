// DEFORM: once per bottle. Its grid's uv runs round the ring and round the figure 8; each
// corner goes where that point of the bottle is, seen through one camera, and hands the
// bottle's normal there down to SHADE in its colour (the bottle's colour comes from `item`).
import xanadu;

/// The camera every bottle is seen through, scaled with the preview (laid out 810 px tall).
Camera camera(float s) { return Camera(1600 * s, 1100 * s); }

/// Where uv is on bottle `b`, in the preview's pixels, and (`normal`) which way it faces there.
/// The figure-8 Klein bottle of xanadu.mesh's Klein, with its normal from the formula's own
/// derivatives rather than four more points, and the bottle's turn from the matrix STEP made.
float3 bottleAt(Bottle b, float2 uv, float s, out float3 normal)
{
    float radius = b.size, thickness = b.size * 0.7;
    float a = (uv.x - 0.5) * TAU, c = uv.y * TAU, h = a * 0.5;
    float sa = sin(a), ca = cos(a), sh = sin(h), ch = cos(h);
    float sc = sin(c), cc = cos(c), s2 = sin(2 * c), c2 = cos(2 * c);
    float across = ch * sc - sh * s2;
    float up = sh * sc + ch * s2;
    float3 outward = float3(sa, 0, -ca);
    float3 local = outward * (radius + thickness * across) - float3(0, thickness * up, 0);

    // How it moves round the ring (a) and round the 8 (c).
    float3 alongA = float3(ca, 0, sa) * (radius + thickness * across) + outward * (thickness * -0.5 * up) - float3(0, thickness * 0.5 * across, 0);
    float3 alongC = outward * (thickness * (ch * cc - 2 * sh * c2)) - float3(0, thickness * (sh * cc + 2 * ch * c2), 0);
    normal = normalize(mul(b.turn, cross(alongC, alongA)));
    return mul(b.turn, local) * s + b.position;
}

[Grid(24, 12)]
ItemVertex deform(ItemVertex v)
{
    float s = v.rest.size.y / 810;
    float3 normal;
    v.place(bottleAt(item, v.uv, s, normal), camera(s));
    v.colour = float4(normal * 0.5 + 0.5, 1);
    return v;
}

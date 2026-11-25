// SHADE: each bottle lit in its own colour, with faint stripes round the 8 so the twist reads.
// The normal comes from DEFORM's corners; the bottle is worked out here once, for where the
// pixel is (the light's angle to the eye), not again for its normal.
import xanadu;

float4 shade(ItemInput i)
{
    float s = i.rest.size.y / 810;
    colour base = colours.oklch(0.74, 0.13, item.hue);
    base.paint(Checker(1.0 / 8).filled(float2(i.uv.y, 0.01), 0.01) * 0.25, colours.white);
    float3 unused;
    SurfacePoint p;
    p.position = bottleAt(item, i.uv, s, unused);
    p.normal = normalize(i.vertexColour.xyz * 2 - 1);
    return lights.studio(p, camera(s).eye, base);
}

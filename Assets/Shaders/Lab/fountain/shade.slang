// SHADE: the lab's GLOW look. A capsule round the particle's spine (a circle when
// still), a soft core and a halo past it, faded by the colour's alpha.
import xanadu;

float4 shade(ItemInput i)
{
    float2 e = extent();
    float r = e.y;
    float2 q = float2(i.local.x - clamp(i.local.x, -e.x, e.x), i.local.y);
    float d = length(q) - r;
    float a = saturate(sdf.filled(d + r * 0.5, 1.5) * 0.8 + sdf.haloed(d + r, r * 2.5, 2.5));
    return float4(i.colour.rgb, i.colour.a * a);
}

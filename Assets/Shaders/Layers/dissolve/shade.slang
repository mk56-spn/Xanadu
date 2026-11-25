// Dissolve layer: eats the colour so far away in noisy patches as amount goes
// 0 to 1, with a glowing rim where it is burning. Scale is the patch size in
// item units; edge the rim's width (0..1 of the amount) and colour.
import xanadu;

[Default(0.35)] uniform float dissolve_amount;
[Default(40)] uniform float dissolve_scale;
[Default(1, 0.55, 0.15, 0.08)] uniform float4 dissolve_edge;

float4 shade(ItemInput i)
{
    float n = noise.fbm(i.local / max(dissolve_scale * unit, 1e-3), 4);
    float cut = dissolve_amount * (1.0 + dissolve_edge.a);
    float left = n - cut;
    if (left < 0.0)
        return float4(i.colour.rgb, 0.0);
    float rim = 1.0 - smoothstep(0.0, max(dissolve_edge.a, 1e-3), left);
    float burning = dissolve_amount > 0.0 ? rim : 0.0;
    return float4(lerp(i.colour.rgb, dissolve_edge.rgb, burning), i.colour.a);
}

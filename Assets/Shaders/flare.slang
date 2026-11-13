// A lens flare worked out per pixel over its box. UV runs from -1 to 1 across
// the box each way; the flare's settings come in per canvas item (instance
// values). It holds still: anything that moves it over time does so from
// outside. Ported from flare.gdshader.
import xanadu;

// Half width, half height (any unit, only their ratio matters), axes (1 or 2), streak thickness.
[Default(1, 1, 1, 0.05)] uniform float4 flare_box;
// Core radius, halo radius, halo brightness, intensity.
[Default(0.15, 0.9, 0.35, 1)] uniform float4 flare_light;
// The flare's colour.
[Default(1, 0.6, 0.25, 1)] uniform float4 flare_tint;
// Streak falloff, heat.
[Default(1.5, 0.7)] uniform float2 flare_shape;

// One streak: bright along its axis, fading to nothing at the box's edge,
// a sharp line inside a softer spread that thins out towards the ends.
float streak(float along, float across, float thickness, float falloff)
{
    float reach = pow(saturate(1.0 - abs(along)), falloff);
    float width = max(thickness * (0.3 + 0.7 * reach), 1e-4);
    float line = exp(-pow(across / width, 2.0));
    float spread = exp(-pow(across / (width * 5.0), 2.0)) * 0.3 * reach;
    return reach * (line + spread);
}

float4 shade(ItemInput i)
{
    float2 uv = i.uv;
    float4 box = flare_box, light = flare_light;
    float2 halfSize = max(box.xy, float2(1e-4));
    float radius = min(halfSize.x, halfSize.y);
    // The pixel in units of the box's shorter half, so round things stay round.
    float2 q = uv * halfSize / radius;
    float d = length(q);

    float thickness = box.w;
    float falloff = flare_shape.x;
    float lines = streak(uv.x, q.y, thickness, falloff);
    if (box.z > 1.5)
        lines += streak(uv.y, q.x, thickness, falloff);

    float core = exp(-pow(d / max(light.x, 1e-3), 2.0) * 2.0);
    float halo = pow(saturate(1.0 - d / max(light.y, 1e-3)), 2.0) * light.z;

    // Nothing reaches the box's edge, so the quad never shows.
    float edge = (1.0 - smoothstep(0.9, 1.0, abs(uv.x))) * (1.0 - smoothstep(0.9, 1.0, abs(uv.y)));
    float energy = (core + lines + halo) * light.w * edge;

    // The core and the streaks' brightest parts run hot, towards white.
    float hot = saturate(core + lines * 0.35) * flare_shape.y;
    float3 tint = lerp(flare_tint.rgb, float3(1.0), hot);

    // The vertex colour carries self modulate and group fades.
    return float4(tint * i.colour.rgb, saturate(energy) * flare_tint.a * i.colour.a);
}

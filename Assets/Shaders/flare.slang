// A lens flare worked out per pixel round the middle of the shape it is drawn
// on. Sizes are in item units (design pixels), turned into screen pixels with
// xif_unit like the rest of the library, so it scales with the item and the
// view, never with the shape's outline. FIT on the item sizes its box to the
// shape; otherwise the box is its own and the shape only clips it. It holds
// still: anything that moves it over time does so from outside. Ported from
// flare.gdshader.
import xanadu;

// Half width, half height of the box the streaks reach across (item units),
// axes (1 or 2), streak thickness (item units). FIT takes the half width and
// height from the shape.
[Fit] [Default(160, 160, 1, 8)] uniform float4 flare_box;
// Core radius, halo radius (item units), halo brightness, intensity.
[Default(24, 144, 0.35, 1)] uniform float4 flare_light;
// The flare's colour.
[Default(1, 0.6, 0.25, 1)] uniform float4 flare_tint;
// Streak falloff, heat.
[Default(1.5, 0.7)] uniform float2 flare_shape;
// Screen pixels per item unit; the drawer sets it.
[Default(1)] uniform float xif_unit;

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
    float unit = max(xif_unit, 1e-4);
    // This pixel from the shape's middle, in item units.
    float2 p = i.canvas.centred / unit;
    float4 box = flare_box, light = flare_light;
    float2 halfSize = max(box.xy, float2(1e-4));
    float d = length(p);

    float thickness = max(box.w, 1e-4);
    float falloff = flare_shape.x;
    float lines = streak(p.x / halfSize.x, p.y, thickness, falloff);
    if (box.z > 1.5)
        lines += streak(p.y / halfSize.y, p.x, thickness, falloff);

    float core = exp(-pow(d / max(light.x, 1e-3), 2.0) * 2.0);
    float halo = pow(saturate(1.0 - d / max(light.y, 1e-3)), 2.0) * light.z;

    // Nothing reaches the shape's edge, so its outline never shows.
    float2 edgeUv = i.canvas.centred / max(i.canvas.size * 0.5, float2(1e-4));
    float edge = (1.0 - smoothstep(0.9, 1.0, abs(edgeUv.x))) * (1.0 - smoothstep(0.9, 1.0, abs(edgeUv.y)));
    float energy = (core + lines + halo) * light.w * edge;

    // The core and the streaks' brightest parts run hot, towards white.
    float hot = saturate(core + lines * 0.35) * flare_shape.y;
    float3 tint = lerp(flare_tint.rgb, float3(1.0), hot);

    // The vertex colour carries self modulate and group fades.
    return float4(tint * i.colour.rgb, saturate(energy) * flare_tint.a * i.colour.a);
}

// Sway layer: bends the shape in the wind, its top most and its bottom not at
// all, as a stalk does (uv.y runs 0 at the top to 1 at the bottom). Each
// instanced copy sways on its own, seeded by its number (uv.x's whole part).
// Stacked over other shaders it only moves; the colour passes through.
import xanadu;

[Default(1)] uniform float xif_unit;
[Default(0)] uniform float xif_seed;
// How far the top bends at full wind (item units), how fast it sways, how hard gusts push (0..1).
[Default(16, 1, 0.5)] uniform float4 sway_wind;

ItemVertex deform(ItemVertex v)
{
    float seed = random(float2(floor(v.uv.x), xif_seed));
    float up = 1.0 - saturate(v.uv.y);
    float bend = looks.wind(v.position, seed, sway_wind.y, sway_wind.z) * sway_wind.x * xif_unit;
    // The top swings out and dips a little as it goes over: bending, not sliding.
    v.position.x += bend * up * up;
    v.position.y += abs(bend) * up * up * up * 0.3;
    return v;
}

float4 shade(ItemInput i)
{
    return i.colour;
}

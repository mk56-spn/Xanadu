// Sway layer: bends the shape in the wind, its top most and its bottom not at
// all, as a stalk does (uv.y runs 0 at the top to 1 at the bottom). Each
// instanced copy sways on its own, seeded by its number (uv.x's whole part).
// Stacked over other shaders it only moves; the colour passes through.
import xanadu;

[Default(0)] uniform float xif_seed;
// How far the top bends at full wind (item units), how fast it sways, how hard gusts push (0..1).
[Default(16, 1, 0.5)] uniform float4 sway_wind;

float4 shade(ItemInput i)
{
    return i.colour;
}

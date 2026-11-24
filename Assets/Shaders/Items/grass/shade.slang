// Grass: an instancer's copies of one blade, each bent by the wind on its own
// (Docs/xif-modules.md, Instancer). It never makes blades, only moves and
// colours them. What a deco hands it: uv.y runs 0 at the blade's tip to 1 at
// its root; uv.x's whole part is the copy's number, its fraction the place
// across the blade; xif_unit is screen pixels per playfield unit, xif_seed
// the instancer's seed.
import xanadu;

[Default(1)] uniform float xif_unit;
[Default(0)] uniform float xif_seed;
// How far a tip bends at full wind (playfield units), how fast blades sway, how hard gusts push (0..1).
[Default(16, 1, 0.5)] uniform float4 grass_wind;
// The blade's colour is multiplied by root at its root, tip at its tip.
[Default(0.5, 0.62, 0.5, 1)] uniform float4 grass_root;
[Default(1.25, 1.3, 1.05, 1)] uniform float4 grass_tip;

float4 shade(ItemInput i)
{
    // From the root's colour to the tip's, most of the change near the tip.
    float up = 1 - saturate(i.uv.y);
    return i.vertexColour * lerp(grass_root, grass_tip, ease.inQuad(up));
}

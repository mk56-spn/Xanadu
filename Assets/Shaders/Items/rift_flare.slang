// The rift's release flare: a white column up and down the field in a blue
// spread, thinning fast. Drawn on a quad the drawer stretches over the column:
// the spread fills it, fading to nothing at its sides (uv.x 0 and 1); the white
// core is its middle sixth. rift_flare.x is how bright it still is (1 to 0).
import xanadu;

[Default(1, 0, 0, 0)] uniform float4 rift_flare;

float4 shade(ItemInput i)
{
    float fast = saturate(rift_flare.x);
    float across = abs(i.uv.x * 2.0 - 1.0);
    colour c = colours.transparent;
    c.paint(1.0, float4(0.4, 0.65, 1.0, 0.5 * fast * (1.0 - across)));
    c.paint(sdf.filled((across - 1.0 / 6.0) / max(fwidth(across), 1e-5)), float4(1.0, 1.0, 1.0, 0.9 * fast));
    return float4(c.rgb * i.colour.rgb, c.a * i.colour.a);
}

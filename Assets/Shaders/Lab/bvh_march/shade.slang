// SHADE: a spark's glow, hottest at its heart; its hue by which one it is.
import xanadu;

float4 shade(ItemInput i)
{
    float r = length(i.uv * 2 - 1);
    float glow = saturate(1.0 - r);
    glow = glow * glow * (0.4 + 2.0 * pow(glow, 6.0));
    colour tint = colours.hue(frac(copy * 0.0011 + 0.1));
    return colour(lerp(colours.white, tint, 0.45).rgb, saturate(glow));
}

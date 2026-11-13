// Cycles the item's hue over time, independent of its own colour.
import xanadu;

[Default(1)] uniform float speed;

float4 shade(ItemInput i)
{
    float4 texel = sampleItem(i.uv);
    return float4(texel.rgb * colours.hue(frac(time * speed)).rgb, texel.a);
}

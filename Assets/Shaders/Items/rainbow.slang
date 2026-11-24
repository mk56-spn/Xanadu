// Cycles the item's hue over time, independent of its own colour.
import xanadu;

[Default(1)] uniform float speed;

float4 shade(ItemInput i)
{
    colour c = sampleItem(i.uv);
    c.rgb *= colours.hue(saw(time * speed)).rgb;
    return c;
}

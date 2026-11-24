// SHADE: each dot a soft disc, in the colour the deform brick gave it.
import xanadu;

float4 shade(ItemInput i)
{
    float cover = Circle(item.size / 2).filled(i.local, 1);
    return float4(i.colour.rgb, 0.85 * cover);
}

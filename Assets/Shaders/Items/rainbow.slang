// Cycles the item's hue over time, independent of its own colour.
import xanadu.stage;
import xanadu.colour;

struct Params { [Default(1)] float speed; };
[[vk::binding(1, 3)]] ConstantBuffer<Params> params;

[shader("fragment")]
float4 shade(ItemVaryings v) : SV_Target
{
    ItemInput i = input(v);
    float4 texel = sampleItem(i.uv);
    return float4(texel.rgb * colours.hue(frac(time * params.speed)).rgb, texel.a);
}

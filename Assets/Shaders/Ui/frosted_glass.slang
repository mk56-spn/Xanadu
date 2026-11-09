// Blurs whatever is drawn behind a translucent panel, so text on it stays
// readable: the screen read at a blurrier mip, with the panel's own colour laid
// over it by its alpha.
import xanadu.stage;

struct Params { [Default(3)] float lod; };
[[vk::binding(1, 3)]] ConstantBuffer<Params> params;

[shader("fragment")]
float4 shade(ItemVaryings v) : SV_Target
{
    ItemInput i = input(v);
    float3 behind = sampleScreen(i.screenUv, params.lod).rgb;
    return float4(lerp(behind, i.colour.rgb, i.colour.a), 1.0);
}

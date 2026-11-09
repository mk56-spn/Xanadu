// Tints white text along a line in the item's own space, from color_from at
// point_from to color_to at point_to, clamped beyond either end. The text's own
// colour still supplies the alpha, so fades keep working.
import xanadu.stage;

struct Params
{
    [Default(1, 1, 1, 1)] float4 color_from;
    [Default(1, 1, 1, 1)] float4 color_to;
    float2 point_from;
    [Default(1, 0)] float2 point_to;
};
[[vk::binding(1, 3)]] ConstantBuffer<Params> params;

[shader("fragment")]
float4 shade(ItemVaryings v) : SV_Target
{
    ItemInput i = input(v);
    float2 line = params.point_to - params.point_from;
    float t = saturate(dot(i.local - params.point_from, line) / dot(line, line));
    float4 tint = lerp(params.color_from, params.color_to, t);
    return float4(tint.rgb * i.colour.rgb, tint.a * i.colour.a);
}

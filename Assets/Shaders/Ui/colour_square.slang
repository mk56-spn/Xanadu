// A colour picker's square: saturation left to right, value bottom to top, at `hue`.
import xanadu.stage;
import xanadu.colour;

struct Params { float hue; };
[[vk::binding(1, 3)]] ConstantBuffer<Params> params;

[shader("fragment")]
float4 shade(ItemVaryings v) : SV_Target
{
    ItemInput i = input(v);
    return colours.hsv(params.hue, i.uv.x, 1.0 - i.uv.y);
}

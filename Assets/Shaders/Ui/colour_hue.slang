// A colour picker's hue bar: red at the top, round the wheel, red again at the bottom.
import xanadu.stage;
import xanadu.colour;

[shader("fragment")]
float4 shade(ItemVaryings v) : SV_Target
{
    ItemInput i = input(v);
    return float4(hsvToRgb(float3(i.uv.y, 1.0, 1.0)), 1.0);
}

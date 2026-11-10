// A colour picker's hue bar: red at the top, round the wheel, red again at the bottom.
import xanadu.stage;
import xanadu.color;

[shader("fragment")]
float4 shade(ItemVaryings v) : SV_Target
{
    ItemInput i = input(v);
    return float4(color.hsv(i.uv.y, 1.0, 1.0), 1.0);
}

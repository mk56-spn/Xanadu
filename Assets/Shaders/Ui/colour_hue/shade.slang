// A colour picker's hue bar: red at the top, round the wheel, red again at the bottom.
import xanadu;

float4 shade(ItemInput i)
{
    return colours.hsv(i.uv.y, 1.0, 1.0);
}

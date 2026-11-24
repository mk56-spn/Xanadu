// A colour picker's square: saturation left to right, value bottom to top, at `hue`.
import xanadu;

uniform float hue;

float4 shade(ItemInput i)
{
    return colours.hsv(hue, i.uv.x, 1.0 - i.uv.y);
}

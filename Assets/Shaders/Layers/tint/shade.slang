// Tint layer: multiplies the colour so far by a colour, then washes it toward
// a flash colour by the flash's alpha (a hit, a highlight).
import xanadu;

[Default(1, 1, 1, 1)] uniform float4 tint_colour;
[Default(1, 1, 1, 0)] uniform float4 tint_flash;

float4 shade(ItemInput i)
{
    float4 tinted = i.colour * tint_colour;
    return float4(lerp(tinted.rgb, tint_flash.rgb, saturate(tint_flash.a)), tinted.a);
}

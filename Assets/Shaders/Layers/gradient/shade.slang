// Gradient layer: multiplies the colour so far by one colour at the bottom and
// another at the top (uv.y runs 0 at the top to 1 at the bottom). Above 1 a
// channel brightens. Curve bends where the change happens: 1 even, 2 late.
import xanadu;

[Default(0.5, 0.62, 0.5, 1)] uniform float4 gradient_bottom;
[Default(1.25, 1.3, 1.05, 1)] uniform float4 gradient_top;
[Default(2)] uniform float gradient_curve;

float4 shade(ItemInput i)
{
    float up = pow(1.0 - saturate(i.uv.y), max(gradient_curve, 0.01));
    return i.colour * lerp(gradient_bottom, gradient_top, up);
}

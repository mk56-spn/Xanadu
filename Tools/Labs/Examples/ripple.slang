// Ripple: what is already drawn under the quad, pushed out by rings spreading
// from the pointer, with red and blue split apart where the rings are steep.
import xanadu;

[Default(18)] uniform float strength;
[Default(60)] uniform float wavelength;

float4 shade(ItemInput i)
{
    float2 fromPointer = i.canvas.centred - mousePos;
    float r = length(fromPointer);
    float2 outward = r > 0 ? fromPointer / r : float2(0);

    // Rings travelling outwards, fading with distance.
    float wave = sin(r / wavelength * TAU - time * 6) * exp(-r / 500);
    // Pixels to screen uv: the screen's size is in the frame, not on ItemInput.
    float2 push = outward * wave * strength / frame.screen;

    float split = 0.4;
    float red = sampleScreen(i.screenUv + push * (1 + split)).r;
    float green = sampleScreen(i.screenUv + push).g;
    float blue = sampleScreen(i.screenUv + push * (1 - split)).b;
    return colour(red, green, blue, 1);
}

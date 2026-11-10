// Plasma: a few sines of position and time summed, coloured by a cosine palette.
import xanadu;

[Default(0.6)] uniform float speed;
[Default(140)] uniform float scale;

float4 shade(ItemInput i)
{
    float2 p = i.canvas.centred / scale;
    float t = time * speed;
    float v = sin(p.x + t)
            + sin(p.y * 1.3 - t * 0.7)
            + sin((p.x + p.y) * 0.7 + t * 1.3)
            + sin(length(p) * 1.5 - t);

    // Inigo Quilez's cosine palette, written out by hand.
    float3 a = float3(0.5), b = float3(0.5), c = float3(1), d = float3(0, 0.33, 0.67);
    float3 rgb = a + b * cos(TAU * (c * v * 0.25 + d));
    return colour(rgb, 1);
}

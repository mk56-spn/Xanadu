// A round dot filling its quad, in dot_tint: a chord line's landing mark.
import xanadu;

[Default(1, 1, 1, 1)] uniform float4 dot_tint;

float4 shade(ItemInput i)
{
    float2 p = i.canvas.centred;
    float radius = min(i.canvas.size.x, i.canvas.size.y) * 0.5;
    float cover = sdf.antialiased(length(p) - radius);
    return float4(dot_tint.rgb * i.colour.rgb, dot_tint.a * cover * i.colour.a);
}
